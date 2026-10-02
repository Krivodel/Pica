using System.Security.Principal;

using Microsoft.Win32;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Services.FileAssociations;

namespace Pica.Desktop.Tests.Services.FileAssociations;

public sealed class WindowsFileAssociationStoreTests
{
    [Theory]
    [InlineData(@"Applications\Pica.exe", @"F:\Pica\Pica.exe", true)]
    [InlineData(@"Applications\Pica.exe", @"F:\Other\Pica.exe", false)]
    [InlineData("png_auto_file", @"F:\Pica\Pica.exe", true)]
    [InlineData("png_auto_file", @"F:\Other\Pica.exe", false)]
    [InlineData("AppX.OtherPhotos", null, false)]
    public void IsPicaDefault_ExistingWindowsAssignments_RecognizesOnlyOwnExecutable(string programId, string? executable, bool expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsFileAssociationStore store = new(Registry.CurrentUser, "S-1-5-21-1001", @"F:\Pica\Pica.exe",
            _ => programId, () => { }, _ => executable);

        bool isPica = store.IsPicaDefault(".png");

        isPica.Should().Be(expected);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("legacy")]
    public void IsPicaDefault_NativeProgramResolution_RecognizesApplicationAndLegacyClasses(string registration)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string suffix = Guid.NewGuid().ToString("N");
        string programId = registration == "application"
            ? $@"Applications\PicaTests{suffix}.exe" : "Pica.Tests.Legacy." + suffix;
        string path = $@"Software\Classes\{programId}";
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing test process path.");
        WindowsFileAssociationStore store = new(Registry.CurrentUser, "S-1-5-21-1001", executable,
            _ => programId, () => { }, WindowsFileAssociationNative.QueryExecutable);

        try
        {
            using RegistryKey command = Registry.CurrentUser.CreateSubKey($@"{path}\shell\open\command");
            command.SetValue("", $"\"{executable}\" \"%1\"");

            bool isPica = store.IsPicaDefault(".png");

            isPica.Should().BeTrue();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path);
        }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".cur")]
    public void RegisterApplication_IsolatedRegistry_QuotesCommandAndPreservesOtherHandlers(string extension)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = $@"Software\Pica.Tests\{Guid.NewGuid():N}";
        using RegistryKey root = Registry.CurrentUser.CreateSubKey(path);
        WindowsFileAssociationStore store = new(root, "S-1-5-21-1001", @"F:\Program Files\Pica\Pica.exe", _ => null, () => { });

        try
        {
            using RegistryKey candidates = root.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
            candidates.SetValue("Other.Image", Array.Empty<byte>(), RegistryValueKind.None);
            candidates.SetValue(PicaFileAssociationService.ProgramId, Array.Empty<byte>(), RegistryValueKind.None);

            store.RegisterApplication(new string[] { extension });
            using RegistryKey? command = root.OpenSubKey($@"{WindowsFileAssociationStore.ProgramPath}\shell\open\command");
            using RegistryKey? capabilities = root.OpenSubKey($@"{WindowsFileAssociationStore.CapabilitiesPath}\FileAssociations");

            command?.GetValue("").Should().Be("\"F:\\Program Files\\Pica\\Pica.exe\" \"%1\"");
            capabilities?.GetValue(extension).Should().Be(PicaFileAssociationService.ProgramId);
            candidates.GetValueNames().Should().Equal("Other.Image");

            store.RemoveApplication();

            candidates.GetValueNames().Should().Equal("Other.Image");
            root.OpenSubKey(WindowsFileAssociationStore.ProgramPath).Should().BeNull();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path);
        }
    }

    [Fact]
    public void GetFallbackProgram_LegacyPicaClassOverride_UsesMachineDefaultAndCanRestoreOverride()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = $@"Software\Pica.Tests\{Guid.NewGuid():N}";
        using RegistryKey root = Registry.CurrentUser.CreateSubKey(path);
        using RegistryKey user = root.CreateSubKey("User");
        using RegistryKey machine = root.CreateSubKey("Machine");
        WindowsFileAssociationStore store = new(user, "S-1-5-21-1001", @"F:\Pica\Pica.exe",
            _ => "png_auto_file", () => { },
            programId => programId == "png_auto_file" ? @"F:\Pica\Pica.exe" : null, machine);

        try
        {
            using RegistryKey userClass = user.CreateSubKey(@"Software\Classes\.png");
            userClass.SetValue("", "png_auto_file");
            userClass.SetValue("Content Type", "image/png");
            using RegistryKey machineClass = machine.CreateSubKey(@"Software\Classes\.png");
            machineClass.SetValue("", "System.Image");

            string? fallback = store.GetFallbackProgram(".png");
            store.SetUserClassProgram(".png", null);
            string? removed = store.GetUserClassProgram(".png");
            store.SetUserClassProgram(".png", "png_auto_file");

            fallback.Should().Be("System.Image");
            removed.Should().BeNull();
            store.GetUserClassProgram(".png").Should().Be("png_auto_file");
            userClass.GetValue("Content Type").Should().Be("image/png");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetUserChoice_EnableDisableEnable_WindowsAcceptsEachChangeAfterEarlierQueries(bool hasOriginalDefault)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string suffix = Guid.NewGuid().ToString("N");
        string extension = ".picatest" + suffix;
        string programId = "Pica.Tests.Image." + suffix;
        string originalProgramId = "Pica.Tests.Original." + suffix;
        string programPath = $@"Software\Classes\{programId}";
        string originalProgramPath = $@"Software\Classes\{originalProgramId}";
        string extensionPath = $@"Software\Classes\{extension}";
        string choicePath = $@"{WindowsFileAssociationStore.FileExtensionsPath}\{extension}";
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("Missing Windows test user SID.");
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing test process path.");
        WindowsFileAssociationStore store = new(Registry.CurrentUser, sid, executable,
            WindowsFileAssociationNative.QueryDefault, WindowsShortcutNative.NotifyAssociationsChanged);

        try
        {
            using RegistryKey command = Registry.CurrentUser.CreateSubKey($@"{programPath}\shell\open\command");
            command.SetValue("", $"\"{executable}\" \"%1\"");
            using RegistryKey originalCommand = Registry.CurrentUser.CreateSubKey($@"{originalProgramPath}\shell\open\command");

            if (hasOriginalDefault)
            {
                originalCommand.SetValue("", $"\"{executable}\" \"%1\"");
            }
            using RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey(extensionPath);

            if (hasOriginalDefault)
            {
                extensionKey.SetValue("", originalProgramId);
            }

            using RegistryKey candidates = extensionKey.CreateSubKey("OpenWithProgids");
            candidates.SetValue(programId, Array.Empty<byte>(), RegistryValueKind.None);
            store.NotifyChanged();
            string? automaticDefault = store.GetDefaultProgram(extension);
            candidates.DeleteValue(programId);
            store.NotifyChanged();

            List<string?> defaults = [store.GetDefaultProgram(extension)];

            foreach (string? choice in new string?[] { programId, originalProgramId, programId, null, programId })
            {
                store.SetUserChoice(extension, choice);
                store.NotifyChanged();
                store.ValidateUserChoice(extension);
                defaults.Add(store.GetDefaultProgram(extension));
            }

            string? fallback = hasOriginalDefault ? originalProgramId : null;
            automaticDefault.Should().Be(hasOriginalDefault ? originalProgramId : programId);
            defaults.Should().Equal(fallback, programId, originalProgramId, programId, fallback, programId);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(choicePath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(programPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(originalProgramPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(extensionPath, throwOnMissingSubKey: false);
            store.NotifyChanged();
        }
    }

    [Theory]
    [InlineData("history")]
    [InlineData("classes")]
    [InlineData("both")]
    public void ClearPicaFallbacks_LegacyPicaCandidates_RepeatedChangesAndRollbackPreserveAssignments(string location)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string suffix = Guid.NewGuid().ToString("N");
        string extension = $".picatest{suffix}";
        string programId = $"Pica.Tests.Legacy.{suffix}";
        string programPath = $@"Software\Classes\{programId}";
        string extensionPath = $@"Software\Classes\{extension}";
        string historyPath = $@"{WindowsFileAssociationStore.FileExtensionsPath}\{extension}";
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("Missing Windows test user SID.");
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing test process path.");
        WindowsFileAssociationStore store = new(Registry.CurrentUser, sid, executable,
            WindowsFileAssociationNative.QueryDefault, WindowsShortcutNative.NotifyAssociationsChanged,
            WindowsFileAssociationNative.QueryExecutable);

        try
        {
            using RegistryKey command = Registry.CurrentUser.CreateSubKey($@"{programPath}\shell\open\command");
            command.SetValue("", $"\"{executable}\" \"%1\"");
            using RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey(extensionPath);
            extensionKey.SetValue("", programId);
            extensionKey.SetValue("ContentType", "image/test");
            using RegistryKey history = Registry.CurrentUser.CreateSubKey($@"{historyPath}\OpenWithProgids");
            using RegistryKey classes = extensionKey.CreateSubKey("OpenWithProgids");
            using RegistryKey recent = Registry.CurrentUser.CreateSubKey($@"{historyPath}\OpenWithList");
            recent.SetValue("a", "Pica.exe");
            recent.SetValue("MRUList", "a");
            history.SetValue("Other.Tests.Image", new byte[] { 1, 2 }, RegistryValueKind.None);
            classes.SetValue("Other.Tests.Image", "retained", RegistryValueKind.String);

            if (location is "history" or "both")
            {
                history.SetValue(programId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            if (location is "classes" or "both")
            {
                classes.SetValue(programId, "", RegistryValueKind.String);
            }

            store.SetUserChoice(extension, programId);
            store.NotifyChanged();
            FileAssociationSnapshot snapshot = store.GetSnapshot(extension);

            store.ClearPicaFallbacks(extension);
            store.SetUserChoice(extension, null);
            store.NotifyChanged();
            bool firstCleared = !store.IsPicaDefault(extension);
            store.SetUserChoice(extension, programId);
            store.NotifyChanged();
            bool reenabled = store.IsPicaDefault(extension);
            store.ClearPicaFallbacks(extension);
            store.SetUserChoice(extension, null);
            store.NotifyChanged();
            bool secondCleared = !store.IsPicaDefault(extension);
            store.RestoreSnapshot(extension, snapshot);
            store.ValidateUserChoice(extension);

            firstCleared.Should().BeTrue();
            reenabled.Should().BeTrue();
            secondCleared.Should().BeTrue();
            snapshot.Matches(store.GetSnapshot(extension)).Should().BeTrue();
            history.GetValue("Other.Tests.Image").Should().BeEquivalentTo(new byte[] { 1, 2 });
            history.GetValueKind("Other.Tests.Image").Should().Be(RegistryValueKind.None);
            classes.GetValue("Other.Tests.Image").Should().Be("retained");
            extensionKey.GetValue("ContentType").Should().Be("image/test");
            recent.GetValue("a").Should().Be("Pica.exe");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(historyPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(programPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(extensionPath, throwOnMissingSubKey: false);
            store.NotifyChanged();
        }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("latest")]
    public void ValidateUserChoice_IncompatibleRecord_RejectsBeforeChangingIt(string record)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = $@"Software\Pica.Tests\{Guid.NewGuid():N}";
        using RegistryKey root = Registry.CurrentUser.CreateSubKey(path);
        WindowsFileAssociationStore store = new(root, "S-1-5-21-1001", "Pica.exe", _ => null, () => { });

        try
        {
            using RegistryKey key = root.CreateSubKey($@"{WindowsFileAssociationStore.FileExtensionsPath}\.png\UserChoice");
            key.SetValue("ProgId", "Other.Image");
            key.SetValue("Hash", "Incompatible hash");

            if (record == "latest")
            {
                using RegistryKey latest = root.CreateSubKey($@"{WindowsFileAssociationStore.FileExtensionsPath}\.png\UserChoiceLatest");
            }

            Action validate = () =>
            {
                if (OperatingSystem.IsWindows())
                {
                    store.ValidateUserChoice(".png");
                }
            };

            validate.Should().Throw<NotSupportedException>();
            key.GetValue("ProgId").Should().Be("Other.Image");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path);
        }
    }

    [Fact]
    public void QueryDefault_UnregisteredExtension_ReturnsNoDefault()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string extension = ".picatest" + Guid.NewGuid().ToString("N");

        string? programId = WindowsFileAssociationNative.QueryDefault(extension);

        programId.Should().BeNull();
    }
}

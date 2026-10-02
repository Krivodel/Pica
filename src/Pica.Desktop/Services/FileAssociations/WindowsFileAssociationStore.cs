using System.Runtime.Versioning;
using System.Security.Principal;

using Microsoft.Win32;

using Pica.Protocol;

namespace Pica.Desktop.Services.FileAssociations;

[SupportedOSPlatform("windows")]
internal sealed class WindowsFileAssociationStore : IPicaFileAssociationStore
{
    internal const string CapabilitiesPath = @"Software\Krivodeling\Pica\Capabilities";
    internal const string FileExtensionsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

    internal static string ProgramPath => $@"Software\Classes\{PicaFileAssociationService.ProgramId}";

    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string OpenWithProgramsKey = "OpenWithProgids";
    private const int MaximumTimestampAttempts = 3;

    private readonly RegistryKey _currentUser;
    private readonly RegistryKey _localMachine;
    private readonly string _userSid;
    private readonly string _executablePath;
    private readonly Func<string, string?> _queryDefault;
    private readonly Func<string, string?> _queryExecutable;
    private readonly Action _notifyChanged;

    public WindowsFileAssociationStore()
        : this(Registry.CurrentUser, GetUserSid(), PicaInstallation.ExecutablePath,
            WindowsFileAssociationNative.QueryDefault, WindowsShortcutNative.NotifyAssociationsChanged,
            WindowsFileAssociationNative.QueryExecutable)
    {
    }

    internal WindowsFileAssociationStore(RegistryKey currentUser, string userSid, string executablePath,
        Func<string, string?> queryDefault, Action notifyChanged, Func<string, string?>? queryExecutable = null,
        RegistryKey? localMachine = null)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _localMachine = localMachine ?? Registry.LocalMachine;
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _userSid = userSid;
        _executablePath = executablePath;
        _queryDefault = queryDefault ?? throw new ArgumentNullException(nameof(queryDefault));
        _queryExecutable = queryExecutable ?? (_ => null);
        _notifyChanged = notifyChanged ?? throw new ArgumentNullException(nameof(notifyChanged));
    }

    public string? GetDefaultProgram(string extension)
    {
        ValidateExtension(extension);

        return _queryDefault(extension);
    }

    public FileAssociationSnapshot GetSnapshot(string extension)
    {
        return new WindowsFileAssociationSnapshot(GetUserChoice(extension), GetDefaultProgram(extension),
            GetUserClassProgram(extension), GetPicaCandidates(extension));
    }

    public string? GetFallbackProgram(string extension)
    {
        string? userProgram = GetUserClassProgram(extension);

        if (!string.IsNullOrWhiteSpace(userProgram) && !IsPicaProgram(userProgram))
        {
            return userProgram;
        }

        using RegistryKey? key = _localMachine.OpenSubKey(GetExtensionClassPath(extension));
        string? machineProgram = key?.GetValue("") as string;

        return !string.IsNullOrWhiteSpace(machineProgram) && !IsPicaProgram(machineProgram)
            ? machineProgram : null;
    }

    public bool IsPicaDefault(string extension)
    {
        return IsPicaProgram(GetDefaultProgram(extension));
    }

    public bool IsPicaProgram(string? programId)
    {
        if (string.Equals(programId, PicaFileAssociationService.ProgramId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(programId))
        {
            return false;
        }

        string? executable = _queryExecutable(programId);

        return executable is not null
            && (string.Equals(executable, _executablePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(executable, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase));
    }

    public void ValidateUserChoice(string extension)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
        {
            throw new NotSupportedException("The installed Windows version uses an unsupported file association algorithm.");
        }

        using RegistryKey? key = _currentUser.OpenSubKey(GetUserChoicePath(extension));
        using RegistryKey? latest = _currentUser.OpenSubKey($@"{FileExtensionsPath}\{extension}\UserChoiceLatest");

        if (latest is not null)
        {
            throw new NotSupportedException($"Windows uses a newer file association record for '{extension}'.");
        }

        if (key is null)
        {
            return;
        }

        string? programId = key.GetValue("ProgId") as string;
        string? hash = key.GetValue("Hash") as string;

        if (string.IsNullOrWhiteSpace(programId) || string.IsNullOrWhiteSpace(hash)
            || !string.Equals(hash, WindowsUserChoiceHash.Calculate(extension, _userSid, programId,
                WindowsFileAssociationNative.GetLastWriteTime(key.Handle)), StringComparison.Ordinal))
        {
            throw new NotSupportedException($"The Windows file association hash for '{extension}' is incompatible.");
        }
    }

    public void SetUserChoice(string extension, string? programId)
    {
        if (programId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programId);
        }

        string parentPath = $@"{FileExtensionsPath}\{ValidateExtension(extension)}";
        using RegistryKey parent = _currentUser.CreateSubKey(parentPath);
        parent.DeleteSubKey("UserChoice", throwOnMissingSubKey: false);

        if (programId is null)
        {
            return;
        }

        using RegistryKey key = parent.CreateSubKey("UserChoice");
        key.SetValue("ProgId", programId, RegistryValueKind.String);

        for (int i = 0; i < MaximumTimestampAttempts; i++)
        {
            long timestamp = WindowsFileAssociationNative.GetLastWriteTime(key.Handle);
            key.SetValue("Hash", WindowsUserChoiceHash.Calculate(extension, _userSid, programId, timestamp), RegistryValueKind.String);
            long actualTimestamp = WindowsFileAssociationNative.GetLastWriteTime(key.Handle);

            if ((timestamp / TimeSpan.TicksPerMinute) == (actualTimestamp / TimeSpan.TicksPerMinute))
            {
                return;
            }
        }

        throw new IOException($"The Windows file association timestamp for '{extension}' did not settle.");
    }

    public void RegisterApplication(IReadOnlyList<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        foreach (string extension in extensions)
        {
            ValidateExtension(extension);
        }

        string command = $"\"{_executablePath}\" \"%1\"";
        using RegistryKey program = _currentUser.CreateSubKey(ProgramPath);
        program.SetValue("", "Изображение Pica");
        using RegistryKey icon = program.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"\"{_executablePath}\",0");
        using RegistryKey open = program.CreateSubKey(@"shell\open\command");
        open.SetValue("", command);
        using RegistryKey capabilities = _currentUser.CreateSubKey(CapabilitiesPath);
        capabilities.SetValue("ApplicationName", PicaProtocolConstants.ApplicationName);
        capabilities.SetValue("ApplicationDescription", "Просмотр изображений в Pica");
        capabilities.SetValue("ApplicationIcon", $"\"{_executablePath}\",0");
        using RegistryKey associations = capabilities.CreateSubKey("FileAssociations");

        foreach (string extension in extensions)
        {
            associations.SetValue(extension, PicaFileAssociationService.ProgramId);
            RemoveAutomaticDefaultCandidate(extension);
        }

        using RegistryKey applications = _currentUser.CreateSubKey(RegisteredApplicationsPath);
        applications.SetValue(PicaProtocolConstants.ApplicationName, CapabilitiesPath);
        NotifyChanged();
    }

    public void ClearPicaFallbacks(string extension)
    {
        if (IsPicaProgram(GetUserClassProgram(extension)))
        {
            SetUserClassProgram(extension, null);
        }

        foreach (WindowsFileAssociationRegistryValue candidate in GetPicaCandidates(extension))
        {
            using RegistryKey? key = _currentUser.OpenSubKey(candidate.KeyPath, writable: true);
            key?.DeleteValue(candidate.ProgramId, throwOnMissingValue: false);
        }
    }

    public void RestoreSnapshot(string extension, FileAssociationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot is not WindowsFileAssociationSnapshot windowsSnapshot)
        {
            throw new ArgumentException("Restoring a Windows file association requires its registry snapshot.", nameof(snapshot));
        }

        if (snapshot.Matches(GetSnapshot(extension)))
        {
            return;
        }

        if (!string.Equals(GetUserClassProgram(extension), snapshot.UserClassProgram, StringComparison.OrdinalIgnoreCase))
        {
            SetUserClassProgram(extension, snapshot.UserClassProgram);
        }

        foreach (WindowsFileAssociationRegistryValue candidate in windowsSnapshot.Candidates)
        {
            using RegistryKey key = _currentUser.CreateSubKey(candidate.KeyPath);
            key.SetValue(candidate.ProgramId, candidate.Data, candidate.Kind);
        }

        SetUserChoice(extension, snapshot.UserChoice);
        NotifyChanged();

        if (!snapshot.Matches(GetSnapshot(extension)))
        {
            throw new IOException($"Windows did not restore the previous default application for '{extension}'.");
        }
    }

    public void NotifyChanged()
    {
        _notifyChanged();
    }

    internal string? GetUserChoice(string extension)
    {
        using RegistryKey? key = _currentUser.OpenSubKey(GetUserChoicePath(extension));

        return key?.GetValue("ProgId") as string;
    }

    internal string? GetUserClassProgram(string extension)
    {
        using RegistryKey? key = _currentUser.OpenSubKey(GetExtensionClassPath(extension));

        return key?.GetValue("") as string;
    }

    internal void SetUserClassProgram(string extension, string? programId)
    {
        string path = GetExtensionClassPath(extension);

        if (programId is null)
        {
            using RegistryKey? key = _currentUser.OpenSubKey(path, writable: true);
            key?.DeleteValue("", throwOnMissingValue: false);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programId);
            using RegistryKey key = _currentUser.CreateSubKey(path);
            key.SetValue("", programId, RegistryValueKind.String);
        }
    }

    internal void RemoveApplication()
    {
        using RegistryKey? commandKey = _currentUser.OpenSubKey($@"{ProgramPath}\shell\open\command");

        if (!string.Equals(commandKey?.GetValue("") as string, $"\"{_executablePath}\" \"%1\"", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using RegistryKey? associations = _currentUser.OpenSubKey($@"{CapabilitiesPath}\FileAssociations");

        foreach (string extension in associations?.GetValueNames() ?? Array.Empty<string>())
        {
            if (string.Equals(GetUserChoice(extension), PicaFileAssociationService.ProgramId, StringComparison.OrdinalIgnoreCase))
            {
                SetUserChoice(extension, null);
            }

            RemoveAutomaticDefaultCandidate(extension);
        }

        using RegistryKey? applications = _currentUser.OpenSubKey(RegisteredApplicationsPath, writable: true);
        applications?.DeleteValue(PicaProtocolConstants.ApplicationName, throwOnMissingValue: false);
        _currentUser.DeleteSubKeyTree(ProgramPath, throwOnMissingSubKey: false);
        _currentUser.DeleteSubKeyTree(CapabilitiesPath, throwOnMissingSubKey: false);
        NotifyChanged();
    }

    private static string GetUserSid()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        return identity.User?.Value
            ?? throw new InvalidOperationException("Windows did not provide the current user identifier.");
    }

    private static string GetUserChoicePath(string extension)
    {
        return $@"{FileExtensionsPath}\{ValidateExtension(extension)}\UserChoice";
    }

    private static string GetExtensionClassPath(string extension)
    {
        return $@"Software\Classes\{ValidateExtension(extension)}";
    }

    private static string ValidateExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        if ((extension.Length < 2) || (extension[0] != '.') || extension.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("A file association requires a plain file extension.", nameof(extension));
        }

        return extension;
    }

    private void RemoveAutomaticDefaultCandidate(string extension)
    {
        // Capabilities advertises support; a sole OpenWithProgids entry also becomes an automatic default.
        using RegistryKey? candidates = _currentUser.OpenSubKey($@"{GetExtensionClassPath(extension)}\{OpenWithProgramsKey}", writable: true);
        candidates?.DeleteValue(PicaFileAssociationService.ProgramId, throwOnMissingValue: false);
    }

    private IReadOnlyList<WindowsFileAssociationRegistryValue> GetPicaCandidates(string extension)
    {
        string[] paths =
        [
            $@"{GetExtensionClassPath(extension)}\{OpenWithProgramsKey}",
            $@"{FileExtensionsPath}\{extension}\{OpenWithProgramsKey}"
        ];
        List<WindowsFileAssociationRegistryValue> candidates = [];

        foreach (string path in paths)
        {
            using RegistryKey? key = _currentUser.OpenSubKey(path);

            foreach (string programId in (key?.GetValueNames() ?? Array.Empty<string>())
                .Where(IsPicaProgram).Order(StringComparer.OrdinalIgnoreCase))
            {
                object data = key?.GetValue(programId, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                    ?? throw new IOException($"The file association candidate '{programId}' changed during preparation.");
                RegistryValueKind kind = key.GetValueKind(programId);
                candidates.Add(new WindowsFileAssociationRegistryValue(path, programId, data, kind));
            }
        }

        return candidates;
    }
}

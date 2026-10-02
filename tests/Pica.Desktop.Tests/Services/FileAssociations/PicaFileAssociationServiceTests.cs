using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Services.FileAssociations;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services.FileAssociations;

public sealed class PicaFileAssociationServiceTests
{
    [Fact]
    public async Task RegisterAsync_AllFormats_AdvertisesEverySupportedExtensionWithoutChangingDefaults()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        PicaFileAssociationService service = CreateService(directory, store);

        await service.RegisterAsync(CancellationToken.None);

        store.RegisteredExtensions.Should().BeEquivalentTo(new ImageFormatRegistry().GetSupportedExtensions());
        store.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_EnableRepeatDisable_RestoresOriginalProgramAndPreservesOtherSettings()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.Choices[".png"] = "Original.Image";
        store.InheritedDefaults[".jpg"] = "Inherited.Image";
        PicaDesktopStateService stateService = CreateStateService(directory);
        await stateService.UpdateAsync(state => state.IsClipboardShortcutEnabled = true, CancellationToken.None);
        PicaFileAssociationService service = new(new ImageFormatRegistry(), store, stateService);

        await service.ApplyAsync(new string[] { ".PNG", ".jpg" }, CancellationToken.None);
        await service.ApplyAsync(new string[] { ".png", ".jpg" }, CancellationToken.None);
        PicaDesktopState enabled = await stateService.LoadAsync(CancellationToken.None);
        await service.ApplyAsync(Array.Empty<string>(), CancellationToken.None);
        PicaDesktopState disabled = await stateService.LoadAsync(CancellationToken.None);

        enabled.PreviousFileAssociations[".png"].Should().Be("Original.Image");
        enabled.PreviousFileAssociations[".jpg"].Should().Be("Inherited.Image");
        store.GetDefaultProgram(".png").Should().Be("Original.Image");
        store.GetDefaultProgram(".jpg").Should().Be("Inherited.Image");
        disabled.PreviousFileAssociations.Should().BeEmpty();
        disabled.IsClipboardShortcutEnabled.Should().BeTrue();
        disabled.HasSeenFileAssociationsPrompt.Should().BeTrue();
        store.Writes.Should().HaveCount(4);
    }

    [Fact]
    public async Task ApplyAsync_ExternalProgramReplacesPica_PreservesExternalChoice()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        PicaFileAssociationService service = CreateService(directory, store);
        await service.ApplyAsync(new string[] { ".png" }, CancellationToken.None);
        store.Choices[".png"] = "Other.Image";

        await service.ApplyAsync(Array.Empty<string>(), CancellationToken.None);

        store.GetDefaultProgram(".png").Should().Be("Other.Image");
    }

    [Fact]
    public async Task ApplyAsync_AllThenRemoveOneThenNoneThenOne_ReopeningPreservesEachSelection()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.PicaProgramIds.Add(@"Applications\Pica.exe");
        store.PicaProgramIds.Add("png_auto_file");
        store.Choices[".bmp"] = @"Applications\Pica.exe";
        store.InheritedDefaults[".bmp"] = "System.Bitmap";
        store.UserClassPrograms[".png"] = "png_auto_file";
        PicaFileAssociationService service = CreateService(directory, store);
        string[] withoutPng = service.SupportedExtensions.Where(extension => extension != ".png").ToArray();

        await service.ApplyAsync(service.SupportedExtensions, CancellationToken.None);
        IReadOnlyList<string> all = await service.LoadSelectionAsync(CancellationToken.None);
        await service.ApplyAsync(withoutPng, CancellationToken.None);
        IReadOnlyList<string> removedOne = await service.LoadSelectionAsync(CancellationToken.None);
        await service.ApplyAsync(Array.Empty<string>(), CancellationToken.None);
        IReadOnlyList<string> none = await service.LoadSelectionAsync(CancellationToken.None);
        await service.ApplyAsync(new string[] { ".png" }, CancellationToken.None);
        IReadOnlyList<string> single = await service.LoadSelectionAsync(CancellationToken.None);

        all.Should().BeEquivalentTo(service.SupportedExtensions);
        removedOne.Should().BeEquivalentTo(withoutPng);
        none.Should().BeEmpty();
        single.Should().Equal(".png");
        store.GetDefaultProgram(".bmp").Should().Be("System.Bitmap");
        store.GetUserClassProgram(".png").Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_RestoreOneFormatAndClearAnother_ReopeningMatchesEverySelection()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.PicaProgramIds.Add(@"Applications\Pica.exe");
        store.PicaProgramIds.Add("heic_auto_file");
        store.UserClassPrograms[".heic"] = "heic_auto_file";
        store.InheritedDefaults[".png"] = "System.Png";
        store.InheritedDefaults[".gif"] = "System.Gif";
        PicaFileAssociationService service = CreateService(directory, store);

        foreach (string extension in service.SupportedExtensions)
        {
            store.Choices[extension] = @"Applications\Pica.exe";
        }

        List<IReadOnlyList<string>> reopenedSelections = [];
        string[][] selections = new string[] { ".png", ".heic", ".gif", ".heic", ".png" }
            .Select(cleared => service.SupportedExtensions.Where(extension => extension != cleared).ToArray()).ToArray();

        foreach (string[] selection in selections)
        {
            await service.ApplyAsync(selection, CancellationToken.None);
            reopenedSelections.Add(await service.LoadSelectionAsync(CancellationToken.None));
        }

        for (int i = 0; i < selections.Length; i++)
        {
            reopenedSelections[i].Should().BeEquivalentTo(selections[i]);
        }
    }

    [Fact]
    public async Task ApplyAsync_PreviousBackupIsLegacyPica_DoesNotRestorePicaWhenUnchecking()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.PicaProgramIds.Add("png_auto_file");
        store.Choices[".png"] = PicaFileAssociationService.ProgramId;
        store.UserClassPrograms[".png"] = "png_auto_file";
        PicaDesktopStateService stateService = CreateStateService(directory);
        await stateService.UpdateAsync(state => state.PreviousFileAssociations[".png"] = "png_auto_file",
            CancellationToken.None);
        PicaFileAssociationService service = new(new ImageFormatRegistry(), store, stateService);

        await service.ApplyAsync(Array.Empty<string>(), CancellationToken.None);

        store.GetDefaultProgram(".png").Should().BeNull();
        IReadOnlyList<string> reopened = await service.LoadSelectionAsync(CancellationToken.None);
        reopened.Should().BeEmpty();
    }

    [Theory]
    [InlineData("registry")]
    [InlineData("settings")]
    public async Task ApplyAsync_DisablingLegacyPicaFails_RestoresUserClassAndDefault(string failure)
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.PicaProgramIds.Add("png_auto_file");
        store.UserClassPrograms[".png"] = "png_auto_file";
        store.Choices[".png"] = PicaFileAssociationService.ProgramId;
        IPicaDesktopStateService stateService = failure == "settings"
            ? new FailingDesktopStateService(new PicaDesktopState()) : CreateStateService(directory);
        PicaFileAssociationService service = new(new ImageFormatRegistry(), store, stateService);

        if (failure == "registry")
        {
            store.AfterWrite = _ =>
            {
                store.AfterWrite = null;
                throw new IOException("Test registry failure after clearing the legacy default");
            };
        }

        Func<Task> apply = () => service.ApplyAsync(Array.Empty<string>(), CancellationToken.None);

        await apply.Should().ThrowAsync<IOException>();
        store.GetUserClassProgram(".png").Should().Be("png_auto_file");
        store.GetUserChoice(".png").Should().Be(PicaFileAssociationService.ProgramId);
        store.IsPicaDefault(".png").Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("failure")]
    [InlineData("ignored")]
    [InlineData("cancel")]
    public async Task ApplyAsync_BatchFails_LeavesDefaultsAndSavedStateUnchanged(string failure)
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.Choices[".apng"] = "Original.Image";
        store.InheritedDefaults[".avif"] = "Inherited.Image";
        using CancellationTokenSource cancellation = new();

        switch (failure)
        {
            case "invalid":
                store.InvalidExtension = ".avif";
                break;
            case "failure":
                store.FailingExtension = ".avif";
                break;
            case "ignored":
                store.IgnoredExtension = ".avif";
                break;
            case "cancel":
                store.AfterWrite = _ => cancellation.Cancel();
                break;
        }

        PicaFileAssociationService service = CreateService(directory, store);

        Func<Task> apply = () => service.ApplyAsync(new string[] { ".apng", ".avif" }, cancellation.Token);

        await apply.Should().ThrowAsync<Exception>();
        store.GetDefaultProgram(".apng").Should().Be("Original.Image");
        store.GetUserChoice(".avif").Should().BeNull();
        PicaDesktopState state = await CreateStateService(directory).LoadAsync(CancellationToken.None);
        state.HasSeenFileAssociationsPrompt.Should().BeFalse();
        state.PreviousFileAssociations.Should().BeEmpty();

        if (failure == "invalid")
        {
            store.Writes.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ApplyAsync_StateSaveFails_RestoresDefaults()
    {
        FakeFileAssociationStore store = new();
        store.Choices[".png"] = "Original.Image";
        FailingDesktopStateService stateService = new(new PicaDesktopState());
        PicaFileAssociationService service = new(new ImageFormatRegistry(), store, stateService);

        Func<Task> apply = () => service.ApplyAsync(new string[] { ".png" }, CancellationToken.None);

        await apply.Should().ThrowAsync<IOException>();
        store.GetDefaultProgram(".png").Should().Be("Original.Image");
    }

    [Fact]
    public async Task ApplyAsync_RollbackAlsoFails_ReportsFailureInsteadOfClaimingRestoration()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.Choices[".apng"] = "Original.Image";
        store.AfterWrite = extension =>
        {
            if (extension == ".avif")
            {
                store.AfterWrite = null;
                store.FailingExtension = ".apng";
                throw new IOException("Test operation and rollback failure");
            }
        };
        PicaFileAssociationService service = CreateService(directory, store);

        Func<Task> apply = () => service.ApplyAsync(new string[] { ".apng", ".avif" }, CancellationToken.None);

        await apply.Should().ThrowAsync<AggregateException>();
        store.GetUserChoice(".avif").Should().BeNull();
        PicaDesktopState state = await CreateStateService(directory).LoadAsync(CancellationToken.None);
        state.HasSeenFileAssociationsPrompt.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_UnsupportedExtension_DoesNotWriteRegistryOrState()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        PicaFileAssociationService service = CreateService(directory, store);

        Func<Task> apply = () => service.ApplyAsync(new string[] { ".exe" }, CancellationToken.None);

        await apply.Should().ThrowAsync<ArgumentException>();
        store.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSelectionAsync_FirstOfferAndReopening_SuggestsAllThenReadsActualDefaults()
    {
        using PicaTemporaryDirectory directory = new();
        FakeFileAssociationStore store = new();
        store.Choices[".png"] = PicaFileAssociationService.ProgramId;
        PicaFileAssociationService service = CreateService(directory, store);

        IReadOnlyList<string> offered = await service.LoadSelectionAsync(CancellationToken.None);
        await service.DismissPromptAsync(CancellationToken.None);
        IReadOnlyList<string> reopened = await service.LoadSelectionAsync(CancellationToken.None);

        offered.Should().BeEquivalentTo(service.SupportedExtensions);
        reopened.Should().Equal(".png");
        store.Writes.Should().BeEmpty();
    }

    private static PicaFileAssociationService CreateService(PicaTemporaryDirectory directory, FakeFileAssociationStore store)
    {
        return new PicaFileAssociationService(new ImageFormatRegistry(), store, CreateStateService(directory));
    }

    private static PicaDesktopStateService CreateStateService(PicaTemporaryDirectory directory)
    {
        return new PicaDesktopStateService(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
    }
}

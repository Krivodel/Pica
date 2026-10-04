using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.Resources;
using Pica.Desktop.Services;
using Pica.Desktop.Tests.ViewModels;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class PicaLocalizationServiceTests
{
    [Fact]
    public async Task CreateLanguageSetting_WithDesktopProvider_PlacesLanguageAtTop()
    {
        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService state = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
        using PicaLocalizationService localization = CreateService(state, directory);
        PicaLanguageSettingContributionProvider provider = new(localization, new RecordingViewModelErrorHandler());

        IReadOnlyList<ViewerSettingContribution> contributions = await provider.CreateAsync(CancellationToken.None);

        contributions.Should().ContainSingle().Which.Placement.Should().Be(ViewerSettingPlacement.Header);
    }

    [Fact]
    public async Task SelectAsync_WithRestart_RestoresLanguageAndPreservesOtherSettings()
    {
        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService state = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
        using PicaLocalizationService first = CreateService(state, directory);
        await first.InitializeAsync(CancellationToken.None);
        string previous = first.Localization.CurrentLocalization?.Id ?? LocalizationConstants.RussianId;

        try
        {
            await first.SelectAsync(LocalizationConstants.EnglishId, CancellationToken.None);
            await state.UpdateAsync(saved => saved.BackgroundIdleTimeoutSeconds = 300, CancellationToken.None);
            using PicaLocalizationService restarted = CreateService(state, directory);
            await restarted.InitializeAsync(CancellationToken.None);
            PicaDesktopState saved = await state.LoadAsync(CancellationToken.None);

            restarted.Localization.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
            DesktopLocalization.Get(PicaDesktopLocalizationKeys.ClipboardShortcutTitle).Should().Be("Global paste");
            saved.LocalizationId.Should().Be(LocalizationConstants.EnglishId);
            saved.BackgroundIdleTimeoutSeconds.Should().Be(300);
        }
        finally
        {
            await first.SelectAsync(previous, CancellationToken.None);
        }
    }

    [Fact]
    public async Task SelectAsync_WhenSavingFails_RestoresPreviousLanguage()
    {
        using PicaTemporaryDirectory directory = new();
        FailingDesktopStateService state = new(new PicaDesktopState
        {
            LocalizationId = LocalizationConstants.RussianId
        });
        using PicaLocalizationService localization = CreateService(state, directory);
        await localization.InitializeAsync(CancellationToken.None);

        Func<Task> select = () => localization.SelectAsync(LocalizationConstants.EnglishId, CancellationToken.None);

        await select.Should().ThrowAsync<IOException>();
        localization.Localization.CurrentLocalization?.Id.Should().Be(LocalizationConstants.RussianId);
        DesktopLocalization.Get(PicaDesktopLocalizationKeys.ClipboardShortcutTitle).Should().Be("Глобальная вставка");
    }

    [Fact]
    public void BuiltInCatalog_WithDesktopKeys_ContainsBothLanguages()
    {
        IReadOnlyList<string> keys = typeof(PicaDesktopLocalizationKeys).GetFields()
            .Select(field => field.GetValue(null)).OfType<string>().Distinct().ToArray();

        DesktopLocalization.Catalog.English.Strings.Keys.Should().Contain(keys);
        DesktopLocalization.Catalog.Russian.Strings.Keys.Should().Contain(keys);
    }

    private static PicaLocalizationService CreateService(IPicaDesktopStateService state, PicaTemporaryDirectory directory)
    {
        return new PicaLocalizationService(state,
            new DirectoryLocalizationFileStore(Path.Combine(directory.DirectoryPath, "Localizations")),
            NullLogger<LocalizationService>.Instance);
    }
}

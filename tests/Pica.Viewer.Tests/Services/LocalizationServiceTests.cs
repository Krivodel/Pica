using System.Globalization;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Krivodeling.Localization.Avalonia;
using Pica.Tests.Common;
using Pica.Viewer.Resources;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class LocalizationServiceTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    [Fact]
    public async Task Refresh_WithCustomTranslation_UsesFilenameIdentityAndEnglishFallback()
    {
        using PicaTemporaryDirectory directory = new();
        await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "Deutsch.json"),
            """{"schemaVersion":1,"culture":"de-DE","strings":{"PicaViewer":{"NoImages":"Keine Bilder","Unknown":"Ignored"}}}""");
        using LocalizationService service = CreateService(directory);

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        service.Select("Deutsch");

        service.CurrentLocalization?.Id.Should().Be("Deutsch");
        service.CurrentCulture.Name.Should().Be("de-DE");
        ViewerLocalization.Get(PicaViewerLocalizationKeys.NoImages).Should().Be("Keine Bilder");
        ViewerLocalization.Get(PicaViewerLocalizationKeys.Copy).Should().Be("Copy");
        service.Get("PicaViewer.Unknown").Should().Be("PicaViewer.Unknown");
        File.Exists(Path.Combine(directory.DirectoryPath, LocalizationConstants.TemplateFileName)).Should().BeTrue();
    }

    [Fact]
    public void BuiltInCatalog_WithRussianAndEnglishResources_ContainsAllDeclaredViewerKeys()
    {
        IReadOnlyList<string> keys = typeof(PicaViewerLocalizationKeys).GetFields()
            .Select(field => field.GetValue(null)).OfType<string>().Distinct().ToArray();

        ViewerLocalization.Catalog.English.Strings.Keys.Should().BeEquivalentTo(keys);
        ViewerLocalization.Catalog.Russian.Strings.Keys.Should().BeEquivalentTo(keys);
    }

    [Fact]
    public async Task LanguageSelection_WhenSearching_DoesNotApplyFilteredLanguage()
    {
        using PicaTemporaryDirectory directory = new();
        using LocalizationService service = CreateService(directory);
        service.Select(LocalizationConstants.EnglishId);
        int applied = 0;
        using ViewerLanguageSettingViewModel model = new(service, (id, _) =>
        {
            applied++;
            service.Select(id);
            return Task.CompletedTask;
        }, new RecordingViewModelErrorHandler());

        model.SearchText = LocalizationConstants.RussianId;
        model.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.EnglishId);
        model.Options.Single(option => option.Localization.Id == LocalizationConstants.EnglishId)
            .IsSearchMatch.Should().BeFalse();
        model.Options.Single(option => option.Localization.Id == LocalizationConstants.RussianId)
            .IsSearchMatch.Should().BeTrue();
        model.SearchText = string.Empty;
        await (model.ApplyCommand.ExecutionTask ?? Task.CompletedTask);

        applied.Should().Be(0);
        model.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.EnglishId);
        model.Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task LanguageSelection_WhenSavingFails_DisplaysSafeErrorAndRestoresSelection()
    {
        using PicaTemporaryDirectory directory = new();
        using LocalizationService service = CreateService(directory);
        service.Select(LocalizationConstants.RussianId);
        IOException failure = new("Private settings path must not be shown.");
        RecordingViewModelErrorHandler errors = new();
        using ViewerLanguageSettingViewModel model = new(service, (_, _) => Task.FromException(failure), errors);

        model.SelectedOption = model.Options.Single(option => option.Localization.Id == LocalizationConstants.EnglishId);
        await (model.ApplyCommand.ExecutionTask ?? Task.CompletedTask);

        model.HasErrorMessage.Should().BeTrue();
        model.ErrorMessage.Should().Be(RecordingViewModelErrorHandler.SafeMessage);
        model.IsLoading.Should().BeFalse();
        model.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.RussianId);
        errors.LastException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task LanguageSelection_WhenStopped_CancelsPendingSaveAndReleasesSubscription()
    {
        using PicaTemporaryDirectory directory = new();
        using LocalizationService service = CreateService(directory);
        service.Select(LocalizationConstants.RussianId);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ViewerLanguageSettingViewModel model = new(service, async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }, new RecordingViewModelErrorHandler());
        model.Start();
        model.SelectedOption = model.Options.Single(option => option.Localization.Id == LocalizationConstants.EnglishId);
        await started.Task;

        model.Stop();
        await (model.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
        service.Select(LocalizationConstants.EnglishId);

        model.IsLoading.Should().BeFalse();
        model.HasErrorMessage.Should().BeFalse();
        model.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.RussianId);
    }

    [Fact]
    public void LanguageSelection_WithTwoModels_SynchronizesSelectedLanguageWithoutSavingTwice()
    {
        using PicaTemporaryDirectory directory = new();
        using LocalizationService service = CreateService(directory);
        service.Select(LocalizationConstants.RussianId);
        int applied = 0;
        Func<string, CancellationToken, Task> select = (id, _) =>
        {
            applied++;
            service.Select(id);
            return Task.CompletedTask;
        };
        using ViewerLanguageSettingViewModel first = new(service, select, new RecordingViewModelErrorHandler());
        using ViewerLanguageSettingViewModel second = new(service, select, new RecordingViewModelErrorHandler());
        first.Start();
        second.Start();

        first.SelectedOption = first.Options.Single(option => option.Localization.Id == LocalizationConstants.EnglishId);

        applied.Should().Be(1);
        first.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.EnglishId);
        second.SelectedOption?.Localization.Id.Should().Be(LocalizationConstants.EnglishId);
    }

    public void Dispose()
    {
        LocalizationSnapshot selected = string.Equals(_originalUiCulture.TwoLetterISOLanguageName, "ru",
            StringComparison.OrdinalIgnoreCase) ? ViewerLocalization.Catalog.Russian : ViewerLocalization.Catalog.English;
        Lang.Avalonia.I18nManager.Instance.Register(
            new LocalizationLangPlugin(new LocalizationTextResolver(selected, ViewerLocalization.Catalog.English)),
            selected.Culture);
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
    }

    private static LocalizationService CreateService(PicaTemporaryDirectory directory)
    {
        return new LocalizationService(new DirectoryLocalizationFileStore(directory.DirectoryPath),
            ViewerLocalization.Catalog, NullLogger<LocalizationService>.Instance);
    }
}

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia;
using FluentAssertions;
using Xunit;

using Krivodeling.Localization.Avalonia;
using Pica.Tests.Common;
using Pica.Viewer.Behaviors;
using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.Controls;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerLanguageSettingControlTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<ViewerTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task SelectLanguage_WithOpenDropDown_ImmediatelyUpdatesExistingLabels()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerLanguageSettingControlTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "English variant.json"),
                """{"schemaVersion":1,"culture":"en-US","strings":{"PicaViewer":{"Settings":"Custom settings"}}}""");
            using LocalizationService localization = new(new DirectoryLocalizationFileStore(directory.DirectoryPath),
                ViewerLocalization.Catalog, NullLogger<LocalizationService>.Instance);
            string original = localization.CurrentCulture.TwoLetterISOLanguageName == "ru"
                ? LocalizationConstants.RussianId : LocalizationConstants.EnglishId;
            localization.Select(LocalizationConstants.RussianId);
            int applied = 0;
            ViewerLanguageSettingContribution contribution = new(
                ViewerLocalization.Get(PicaViewerLocalizationKeys.Settings), localization, (id, _) =>
            {
                applied++;
                localization.Select(id);
                return Task.CompletedTask;
            }, new RecordingViewModelErrorHandler(), PicaViewerLocalizationKeys.Settings)
            {
                LocalizationKey = PicaViewerLocalizationKeys.Settings
            };
            ViewerSettingsContentControl content = new(new ViewerSettingContribution[] { contribution });
            Window window = new() { Content = content };

            try
            {
                window.Show();
                window.UpdateLayout();
                ComboBox languages = content.GetVisualDescendants().OfType<ComboBox>().Single();
                StackPanel root = content.Content.Should().BeOfType<StackPanel>().Subject;
                StackPanel section = root.Children[0].Should().BeOfType<StackPanel>().Subject;
                TextBlock label = section.Children[0].Should().BeOfType<TextBlock>().Subject;

                await SelectLanguageAsync(languages, content, LocalizationConstants.EnglishId);

                applied.Should().Be(1);
                localization.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
                ViewerLocalization.Get(PicaViewerLocalizationKeys.Settings).Should().Be("Settings");
                label.Text.Should().Be("Settings");

                await SelectLanguageAsync(languages, content, "English variant");

                applied.Should().Be(2);
                localization.CurrentLocalization?.Id.Should().Be("English variant");
                label.Text.Should().Be("Custom settings");

                await SelectLanguageAsync(languages, content, LocalizationConstants.RussianId);

                applied.Should().Be(3);
                label.Text.Should().Be("Настройки");
            }
            finally
            {
                window.Close();
                localization.Select(original);
            }
        });
    }

    [Fact]
    public async Task Search_WithClearCloseAndReopen_PreservesSelectionAndRestoresFocus()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerLanguageSettingControlTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using LocalizationService localization = new(new DirectoryLocalizationFileStore(directory.DirectoryPath),
                ViewerLocalization.Catalog, NullLogger<LocalizationService>.Instance);
            string original = localization.CurrentCulture.TwoLetterISOLanguageName == "ru"
                ? LocalizationConstants.RussianId : LocalizationConstants.EnglishId;
            localization.Select(LocalizationConstants.EnglishId);
            int applied = 0;
            using ViewerLanguageSettingViewModel model = new(localization, (id, _) =>
            {
                applied++;
                localization.Select(id);
                return Task.CompletedTask;
            }, new RecordingViewModelErrorHandler());
            ViewerLanguageSettingControl setting = new("Language", model, PicaViewerLocalizationKeys.Settings);
            Button otherControl = new() { Content = "Other control" };
            StackPanel content = new();
            content.Children.Add(setting.Control);
            content.Children.Add(otherControl);
            Window window = new() { Content = content, Width = 400, Height = 300 };

            try
            {
                window.Show();
                window.CaptureRenderedFrame();
                setting.Control.GetVisualDescendants().OfType<TextBox>().Should().BeEmpty();
                ComboBox languages = setting.Control.GetVisualDescendants().OfType<ComboBox>().Single();
                ViewerLanguageOptionViewModel? selected = model.SelectedOption;

                languages.IsDropDownOpen = true;
                await setting.Completion;
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();
                Control popupContent = GetPopupContent(languages);
                TextBox search = popupContent.GetVisualDescendants().OfType<TextBox>().Single();
                PathIcon icon = search.GetVisualDescendants().OfType<PathIcon>()
                    .Single(candidate => candidate.Classes.Contains("search-icon"));

                search.PlaceholderText.Should().Be("Settings");
                search.IsFocused.Should().BeTrue();
                icon.IsEffectivelyVisible.Should().BeTrue();
                icon.IsHitTestVisible.Should().BeFalse();
                icon.Margin.Left.Should().Be(6d);
                search.GetVisualDescendants().OfType<Button>().Should().NotContain(button => button.IsEffectivelyVisible);

                search.Text = "РУС";
                window.CaptureRenderedFrame();
                Button clear = search.GetVisualDescendants().OfType<Button>().Single();

                popupContent.GetVisualDescendants().OfType<ComboBoxItem>()
                    .Single(item => item.Content is ViewerLanguageOptionViewModel option
                        && option.Localization.Id == LocalizationConstants.EnglishId).IsVisible.Should().BeFalse();
                popupContent.GetVisualDescendants().OfType<ComboBoxItem>()
                    .Single(item => item.Content is ViewerLanguageOptionViewModel option
                        && option.Localization.Id == LocalizationConstants.RussianId).IsEffectivelyVisible.Should().BeTrue();
                model.SelectedOption.Should().BeSameAs(selected);
                languages.SelectedItem.Should().BeSameAs(selected);
                applied.Should().Be(0);
                clear.IsEffectivelyVisible.Should().BeTrue();

                Point clearCenter = clear.TranslatePoint(new Point(clear.Bounds.Width / 2d, clear.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The clear button must be attached to the window.");
                window.MouseMove(clearCenter);
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();

                clear.IsPointerOver.Should().BeTrue();
                search.GetSelfAndVisualDescendants().OfType<Control>()
                    .Should().OnlyContain(control => !ToolTip.GetServiceEnabled(control));

                clear.Focus().Should().BeTrue();
                clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await search.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                search.Text.Should().BeEmpty();
                model.SearchText.Should().BeEmpty();
                model.Options.Should().OnlyContain(option => option.IsSearchMatch);
                search.IsFocused.Should().BeTrue();

                search.Text = "Рус";
                languages.IsDropDownOpen = false;
                otherControl.Focus().Should().BeTrue();
                await search.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                model.SearchText.Should().BeEmpty();
                model.Options.Should().OnlyContain(option => option.IsSearchMatch);
                TextBoxFocusBehavior.GetFocusOnClear(search).Should().BeFalse();
                search.IsFocused.Should().BeFalse();
                otherControl.IsFocused.Should().BeTrue();

                languages.IsDropDownOpen = true;
                await setting.Completion;
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();

                search.Text.Should().BeEmpty();
                search.IsFocused.Should().BeTrue();
                model.SelectedOption.Should().BeSameAs(selected);
                applied.Should().Be(0);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                localization.Select(original);
            }
        });
    }

    private static Control GetPopupContent(ComboBox languages)
    {
        return languages.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "PART_Popup").Child
            ?? throw new InvalidOperationException("The language popup must have content.");
    }

    private static async Task SelectLanguageAsync(ComboBox languages, ViewerSettingsContentControl content, string id)
    {
        languages.IsDropDownOpen = true;
        await content.Completion;
        Dispatcher.UIThread.RunJobs();
        languages.IsDropDownOpen.Should().BeTrue();
        languages.IsEnabled.Should().BeTrue();

        languages.SelectedItem = languages.Items.OfType<ViewerLanguageOptionViewModel>()
            .Single(option => option.Localization.Id == id);
        languages.IsDropDownOpen = false;
        await content.Completion;
        Dispatcher.UIThread.RunJobs();
    }
}

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.Resources;
using Pica.Desktop.Tests.ViewModels;
using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Styles;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class ComboBoxStylesTests
{
    private static readonly Color SelectedItemBackgroundColor =
        Color.Parse("#214575");
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task LanguageDropDown_WithDesktopStyles_UsesOneAnimationAndPreservesSearchFocus()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ComboBoxStylesTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            using LocalizationService localization = new(new DirectoryLocalizationFileStore(directory.DirectoryPath),
                DesktopLocalization.Catalog, NullLogger<LocalizationService>.Instance);
            string original = localization.CurrentCulture.TwoLetterISOLanguageName == "ru"
                ? LocalizationConstants.RussianId : LocalizationConstants.EnglishId;
            ViewerLanguageSettingContribution language = new("Language", localization, (_, _) => Task.CompletedTask,
                new RecordingViewModelErrorHandler(), PicaViewerLocalizationKeys.Settings);
            ViewerSettingsContentControl settings = new(new ViewerSettingContribution[] { language });
            ComboBox ordinary = new() { ItemsSource = new string[] { "x1", "x2" }, SelectedIndex = 0 };
            StackPanel content = new();
            content.Children.Add(settings);
            content.Children.Add(ordinary);
            Window window = new() { Content = content, Width = 400, Height = 300 };

            try
            {
                window.Show();
                window.CaptureRenderedFrame();
                ComboBox languages = settings.GetVisualDescendants().OfType<ComboBox>().Single();
                languages.IsDropDownOpen = true;
                await settings.Completion;
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();
                Control popup = languages.GetVisualDescendants().OfType<Popup>().Single().Child
                    ?? throw new InvalidOperationException("The language popup must have content.");
                TextBox search = popup.GetVisualDescendants().OfType<TextBox>().Single();

                GetActiveAnimationStyles(popup).Should().ContainSingle().Which.Animations.Should().HaveCount(3);
                search.IsFocused.Should().BeTrue();

                search.Text = "Рус";
                window.CaptureRenderedFrame();
                Button clear = search.GetVisualDescendants().OfType<Button>().Single();
                Point clearCenter = clear.TranslatePoint(new Point(clear.Bounds.Width / 2d, clear.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The clear button must be attached to the window.");
                window.MouseMove(clearCenter);
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();

                clear.IsPointerOver.Should().BeTrue();
                Control[] tooltipOwners = search.GetSelfAndVisualDescendants().OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible && (ToolTip.GetTip(control) is not null))
                    .ToArray();
                tooltipOwners.Should().NotBeEmpty();
                tooltipOwners.Should().OnlyContain(control => !ToolTip.GetServiceEnabled(control));

                languages.IsDropDownOpen = false;
                Dispatcher.UIThread.RunJobs();

                GetActiveAnimationStyles(popup).Should().ContainSingle().Which.Animations.Should().HaveCount(1);

                ordinary.IsDropDownOpen = true;
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();
                Control ordinaryPopup = ordinary.GetVisualDescendants().OfType<Popup>().Single().Child
                    ?? throw new InvalidOperationException("The ordinary popup must have content.");

                GetActiveAnimationStyles(ordinaryPopup).Should().ContainSingle().Which.Animations.Should().HaveCount(3);
            }
            finally
            {
                window.Close();
                localization.Select(original);
            }
        });
    }

    [Fact]
    public async Task SelectedItem_WhenDropDownOpened_UsesBlueOverrideBackground()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ComboBoxStylesTests),
            SessionLock,
            () =>
            {
                ComboBox comboBox = new()
                {
                    ItemsSource = new string[] { "x1", "x2", "x3", "x4" },
                    SelectedIndex = 1
                };
                UserControl view = new()
                {
                    Content = comboBox
                };
                Window window = new()
                {
                    Width = 320d,
                    Height = 240d,
                    Content = view
                };

                try
                {
                    window.Show();
                    comboBox.IsDropDownOpen = true;
                    Dispatcher.UIThread.RunJobs();

                    ComboBoxItem selectedItem = comboBox.ContainerFromIndex(1)
                        as ComboBoxItem
                        ?? throw new InvalidOperationException(
                            "The selected combo-box item was not realized.");
                    Border itemBackground = selectedItem
                        .GetVisualDescendants()
                        .OfType<Border>()
                        .Single(border => string.Equals(
                            border.Name,
                            "BorderBasicStyle",
                            StringComparison.Ordinal));
                    ISolidColorBrush background = itemBackground.Background
                        as ISolidColorBrush
                        ?? throw new InvalidOperationException(
                            "The selected combo-box item has no solid background.");

                    background.Color.Should().Be(SelectedItemBackgroundColor);
                }
                finally
                {
                    window.Close();
                }
            });
    }

    private static IReadOnlyList<Style> GetActiveAnimationStyles(Control popup)
    {
        List<Style> animations = [];

        foreach (LayoutTransformControl panel in popup.GetSelfAndVisualDescendants().OfType<LayoutTransformControl>())
        {
            ValueStoreDiagnostic diagnostic = panel.GetValueStoreDiagnostic();
            // Avalonia exposes the style frames at runtime but omits them from its reference assembly.
            System.Collections.IEnumerable frames = diagnostic.GetType().GetProperty("AppliedFrames")?.GetValue(diagnostic)
                as System.Collections.IEnumerable
                ?? throw new InvalidOperationException("Avalonia must expose the applied style frames for diagnostics.");

            foreach (object frame in frames)
            {
                Type frameType = frame.GetType();

                if ((frameType.GetProperty("IsActive")?.GetValue(frame) is true)
                    && (frameType.GetProperty("Source")?.GetValue(frame) is Style style)
                    && (style.Animations.Count > 0))
                {
                    animations.Add(style);
                }
            }
        }

        return animations;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Tests.ViewModels;
using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Views;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class FileAssociationsWindowTests
{
    private const double PositionTolerance = 0.1;
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Theory]
    [InlineData(620, true)]
    [InlineData(460, false)]
    public async Task Menu_DefaultAndNarrowWidths_KeepRelatedFormatsTogetherAndScrollOnlyWhenNeeded(int width, bool fitsWithoutScrolling)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(FileAssociationsWindowTests), SessionLock, async () =>
        {
            FakeFileAssociationService service = new()
            {
                SupportedExtensions = new ImageFormatRegistry().GetSupportedExtensions().Order(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            FileAssociationsViewModel viewModel = new(service, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
            FileAssociationsWindow window = new(viewModel) { Width = width };
            (string First, string Second)[] relatedExtensions =
            [
                (".jpeg", ".jpg"),
                (".tif", ".tiff"),
                (".heic", ".heif"),
                (".apng", ".png"),
                (".cur", ".ico")
            ];

            try
            {
                window.Show();
                await viewModel.LoadCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                foreach ((string firstExtension, string secondExtension) in relatedExtensions)
                {
                    CheckBox first = GetFormatCheckBox(window, firstExtension);
                    CheckBox second = GetFormatCheckBox(window, secondExtension);
                    Point firstPosition = first.TranslatePoint(default, window)
                        ?? throw new InvalidOperationException("Missing first format position.");
                    Point secondPosition = second.TranslatePoint(default, window)
                        ?? throw new InvalidOperationException("Missing second format position.");

                    secondPosition.Y.Should().BeApproximately(firstPosition.Y, PositionTolerance,
                        "{0} and {1} belong together", firstExtension, secondExtension);
                    secondPosition.X.Should().BeGreaterThan(firstPosition.X);
                    (secondPosition.X + second.Bounds.Width).Should().BeLessThanOrEqualTo(window.ClientSize.Width);
                }

                CheckBox jpeg = GetFormatCheckBox(window, ".jpeg");
                ScrollViewer formatList = jpeg.GetVisualAncestors().OfType<ScrollViewer>().First();
                (formatList.Extent.Height <= formatList.Viewport.Height + PositionTolerance).Should().Be(fitsWithoutScrolling,
                    "the format list is {0} pixels high and its viewport is {1} pixels high", formatList.Extent.Height, formatList.Viewport.Height);
                double firstColumnPosition = GetFormatCheckBox(window, ".apng").TranslatePoint(default, window)?.X
                    ?? throw new InvalidOperationException("Missing first column position.");
                viewModel.FilterText = "JPEG";
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                window.GetVisualDescendants().OfType<CheckBox>().Where(checkBox => checkBox.IsVisible)
                    .Should().ContainSingle().Which.Should().BeSameAs(jpeg);
                jpeg.TranslatePoint(default, window)?.X.Should().BeApproximately(firstColumnPosition, PositionTolerance);

                jpeg.IsChecked = true;
                viewModel.FilterText = "";
                Dispatcher.UIThread.RunJobs();

                viewModel.Formats.Single(format => string.Equals(format.Extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                    .IsSelected.Should().BeTrue();
                window.GetVisualDescendants().OfType<CheckBox>().Should().HaveCount(service.SupportedExtensions.Count)
                    .And.OnlyContain(checkBox => checkBox.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(620, 580)]
    [InlineData(460, 540)]
    public async Task Menu_SelectionErrorsAndEscape_UsesBindingsRedErrorAndKeyboardClose(int width, int height)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(FileAssociationsWindowTests), SessionLock, async () =>
        {
            FakeFileAssociationService service = new()
            {
                SupportedExtensions = new ImageFormatRegistry().GetSupportedExtensions().Order(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            FileAssociationsViewModel viewModel = new(service, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
            FileAssociationsWindow window = new(viewModel) { Width = width, Height = height };

            try
            {
                window.Show();
                await viewModel.LoadCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                CheckBox png = GetFormatCheckBox(window, ".png");
                Button apply = window.FindControl<Button>("ApplyFormats")
                    ?? throw new InvalidOperationException("Missing apply button.");

                png.IsChecked.Should().BeTrue();
                apply.IsEnabled.Should().BeTrue();
                apply.Bounds.Width.Should().BeGreaterThan(0);
                apply.TranslatePoint(new Point(0, apply.Bounds.Height), window)?.Y.Should().BeLessThanOrEqualTo(height);

                service.Failure = new NotSupportedException("Private registry error");
                await viewModel.ApplyCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                TextBlock error = window.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("viewer-error"));

                error.IsVisible.Should().BeTrue();
                error.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Color.Parse("#FFE5484D"));

                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

                window.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static CheckBox GetFormatCheckBox(FileAssociationsWindow window, string extension)
    {
        return window.GetVisualDescendants().OfType<CheckBox>()
            .Single(checkBox => checkBox.DataContext is FileAssociationFormatViewModel format
                && string.Equals(format.Extension, extension, StringComparison.OrdinalIgnoreCase));
    }
}

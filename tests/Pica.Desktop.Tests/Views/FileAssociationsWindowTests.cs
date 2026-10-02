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
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Theory]
    [InlineData(620, 670)]
    [InlineData(460, 540)]
    public async Task Menu_SelectionErrorsAndEscape_UsesBindingsRedErrorAndKeyboardClose(int width, int height)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(FileAssociationsWindowTests), SessionLock, async () =>
        {
            FakeFileAssociationService service = new()
            {
                SupportedExtensions = new ImageFormatRegistry().GetSupportedExtensions().Order(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            FileAssociationsViewModel viewModel = new(service, new RecordingViewModelErrorHandler());
            FileAssociationsWindow window = new(viewModel) { Width = width, Height = height };

            try
            {
                window.Show();
                await viewModel.LoadCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                CheckBox png = window.GetVisualDescendants().OfType<CheckBox>()
                    .Single(checkBox => checkBox.DataContext is FileAssociationFormatViewModel { Extension: ".png" });
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
}

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Controls;

namespace Pica.Viewer.Tests.Controls;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerSettingErrorControlTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task ShowError_WithWindowStyle_DisplaysRedTextOutsideViewerContent()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerSettingErrorControlTests), SessionLock, () =>
        {
            ViewerSettingErrorControl error = new(_ => "Safe error", NullLogger.Instance);
            Window window = new() { Content = error, Foreground = Brushes.White };
            Uri source = new("avares://Pica.Viewer/Resources/ViewerErrorStyles.axaml");
            window.Styles.Add(new StyleInclude(source) { Source = source });
            window.Show();

            error.ShowError(new IOException("Private error"));

            error.IsVisible.Should().BeTrue();
            error.Text.Should().Be("Safe error");
            SolidColorBrush foreground = error.Foreground.Should().BeOfType<SolidColorBrush>().Subject;
            foreground.Color.R.Should().BeGreaterThan(foreground.Color.G);
            foreground.Color.R.Should().BeGreaterThan(foreground.Color.B);
            window.Close();
        });
    }
}

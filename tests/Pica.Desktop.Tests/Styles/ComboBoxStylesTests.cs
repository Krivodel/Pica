using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Styles;

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
}

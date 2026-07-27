using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Pica.Protocol;

namespace Pica.Desktop.Views;

internal sealed class StartupErrorWindow : Window
{
    private const double WindowWidth = 520d;
    private const double WindowHeight = 180d;

    public StartupErrorWindow()
    {
        Width = WindowWidth;
        Height = WindowHeight;
        CanResize = false;
        Title = PicaProtocolConstants.ApplicationName;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = new Border
        {
            Padding = new Thickness(24d),
            Child = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = $"Не удалось запустить {PicaProtocolConstants.ApplicationName}. "
                    + "Переустанови приложение, собери версию для своей ОС или пожалуйся разрабу.",
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }
}

using Avalonia.Controls;

namespace Pica.Desktop.Views;

internal static class DesktopDialogPresenter
{
    internal static async Task ShowAsync(Window window, Window owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(owner);
        ct.ThrowIfCancellationRequested();

        if (owner.Screens.ScreenFromWindow(owner) is { } screen)
        {
            double availableWidth = screen.WorkingArea.Width / screen.Scaling;
            double availableHeight = screen.WorkingArea.Height / screen.Scaling;
            window.MinWidth = Math.Min(window.MinWidth, availableWidth);
            window.MinHeight = Math.Min(window.MinHeight, availableHeight);
            window.MaxWidth = availableWidth;
            window.MaxHeight = availableHeight;
            window.Width = Math.Min(window.Width, availableWidth);
            window.Height = Math.Min(window.Height, availableHeight);
        }

        using CancellationTokenRegistration cancellation = ct.Register(() => window.Dispatcher.Post(window.Close));
        await window.ShowDialog(owner);
    }
}

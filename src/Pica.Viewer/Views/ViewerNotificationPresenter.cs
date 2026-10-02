using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;

using SukiUI.Controls;
using SukiUI.Enums;
using SukiUI.Toasts;

namespace Pica.Viewer.Views;

internal sealed class ViewerNotificationPresenter : IDisposable
{
    private static readonly TimeSpan ErrorDisplayDuration = TimeSpan.FromSeconds(4d);
    private readonly SukiWindow _owner;
    private readonly SukiToastManager _manager = new();
    private readonly SukiToastHost _host;
    private ISukiToast? _toast;

    internal ViewerNotificationPresenter(SukiWindow owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _host = new SukiToastHost
        {
            Manager = _manager,
            MaxToasts = 1,
            Position = ToastLocation.BottomLeft
        };
        owner.Hosts.Add(_host);
    }

    public void Dispose()
    {
        if (_toast is not null)
        {
            _manager.Dismiss(_toast);
            _toast = null;
        }

        _owner.Hosts.Remove(_host);
    }

    internal void ShowError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (_toast is not null)
        {
            _manager.Dismiss(_toast);
        }

        TextBlock content = new() { Text = message, TextWrapping = TextWrapping.Wrap };
        content.Classes.Add("viewer-error");
        _toast = _manager.CreateToast()
            .WithContent(content)
            .OfType(NotificationType.Error)
            .Dismiss().After(ErrorDisplayDuration)
            .Queue();
    }
}

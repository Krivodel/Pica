using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;

using SukiUI.Controls;
using SukiUI.Enums;
using SukiUI.Toasts;

using Pica.Desktop.Resources;
using Pica.Desktop.Services.Updates;

namespace Pica.Desktop.Views.Updates;

internal sealed class ApplicationUpdateToastPresenter : IDisposable
{
    public event EventHandler? UpdateRequested;
    public event EventHandler? LaterRequested;

    private readonly ISukiToastManager _manager;
    private SukiWindow? _owner;
    private SukiToastHost? _host;
    private ApplicationUpdatePresentation? _presentation;
    private ISukiToast? _toast;
    private TextBlock? _messageText;
    private ProgressBar? _progressBar;
    private bool _isActionToast;
    private bool _isDisposed;

    public ApplicationUpdateToastPresenter(ISukiToastManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public void Attach(SukiWindow owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_owner is not null)
        {
            throw new InvalidOperationException(
                "The Pica update notification presenter is already attached.");
        }

        SukiToastHost host = new()
        {
            Manager = _manager,
            MaxToasts = 1,
            Position = ToastLocation.BottomRight
        };
        owner.Hosts.Add(host);
        _owner = owner;
        _host = host;
    }

    public void Detach()
    {
        Dismiss();

        if ((_owner is not null) && (_host is not null))
        {
            _owner.Hosts.Remove(_host);
        }

        _owner = null;
        _host = null;
        _presentation = null;
    }

    public void ShowAvailable(string version)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _presentation = new ApplicationUpdatePresentation(version);
        ShowActionToast();
    }

    public void ShowDownloadProgress(int progress)
    {
        ApplicationUpdatePresentation presentation = GetPresentation();
        presentation.ShowDownloadProgress(progress);

        if ((_toast is null) || _isActionToast)
        {
            ShowProgressToast();
            return;
        }

        RefreshContent();
    }

    public void ShowInstalling()
    {
        ApplicationUpdatePresentation presentation = GetPresentation();
        presentation.ShowInstalling();

        if ((_toast is null) || _isActionToast)
        {
            ShowProgressToast();
            return;
        }

        RefreshContent();
    }

    public void ShowInstallFailure()
    {
        GetPresentation().ShowInstallFailure();
        ShowActionToast();
    }

    public void Dismiss()
    {
        if ((_toast is not null) && !_manager.IsDismissed(_toast))
        {
            _manager.Dismiss(_toast);
        }

        _toast = null;
        _messageText = null;
        _progressBar = null;
        _isActionToast = false;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Detach();
        _isDisposed = true;
    }

    private ApplicationUpdatePresentation GetPresentation()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return _presentation
            ?? throw new InvalidOperationException(
                "The Pica update notification has not been initialized.");
    }

    private void ShowActionToast()
    {
        Dismiss();
        StackPanel content = CreateContent();
        ISukiToast toast = CreateToastBuilder(content)
            .WithActionButton(
                DesktopUiStrings.UpdateLater,
                OnLaterRequested,
                true,
                SukiButtonStyles.Basic)
            .WithActionButton(
                DesktopUiStrings.UpdateInstall,
                OnUpdateRequested,
                true)
            .Queue();
        Present(toast, isActionToast: true);
    }

    private void ShowProgressToast()
    {
        Dismiss();
        StackPanel content = CreateContent();
        ISukiToast toast = CreateToastBuilder(content)
            .WithLoadingState(true)
            .Queue();
        Present(toast, isActionToast: false);
    }

    private SukiToastBuilder CreateToastBuilder(StackPanel content)
    {
        return _manager
            .CreateToast()
            .WithTitle(DesktopUiStrings.UpdateTitle)
            .WithContent(content)
            .OfType(NotificationType.Information);
    }

    private StackPanel CreateContent()
    {
        _messageText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        _progressBar = new ProgressBar
        {
            Maximum = 100d,
            MinWidth = 280d,
            ShowProgressText = true
        };

        return new StackPanel
        {
            Spacing = 8d,
            Children =
            {
                _messageText,
                _progressBar
            }
        };
    }

    private void Present(ISukiToast toast, bool isActionToast)
    {
        _toast = toast;
        _isActionToast = isActionToast;
        RefreshContent();
    }

    private void RefreshContent()
    {
        ApplicationUpdatePresentation presentation = GetPresentation();

        if (_messageText is not null)
        {
            _messageText.Text = presentation.Message;
        }

        if (_progressBar is not null)
        {
            _progressBar.IsVisible = presentation.IsProgressVisible;
            _progressBar.IsIndeterminate = presentation.IsProgressIndeterminate;
            _progressBar.Value = presentation.DownloadProgress;
        }

        if (_toast is not null)
        {
            _toast.LoadingState = presentation.IsProgressVisible;
        }
    }

    private void OnUpdateRequested(ISukiToast toast)
    {
        _ = toast;
        UpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLaterRequested(ISukiToast toast)
    {
        _ = toast;
        LaterRequested?.Invoke(this, EventArgs.Empty);
    }
}

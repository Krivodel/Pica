using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Pica.Desktop.Resources;
using Pica.Desktop.Services.Updates;

namespace Pica.Desktop.Views;

internal sealed partial class ApplicationUpdateWindow : Window
{
    public event EventHandler? UpdateRequested;
    public event EventHandler? LaterRequested;

    private readonly TextBlock _messageText;
    private readonly ProgressBar _progressBar;
    private readonly Button _updateButton;
    private readonly Button _laterButton;
    private readonly ApplicationUpdatePresentation _presentation;
    private bool _isShutdownCloseRequested;

    public ApplicationUpdateWindow(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        AvaloniaXamlLoader.Load(this);
        _messageText = GetRequiredControl<TextBlock>("MessageText");
        _progressBar = GetRequiredControl<ProgressBar>("DownloadProgressBar");
        _updateButton = GetRequiredControl<Button>("UpdateButton");
        _laterButton = GetRequiredControl<Button>("LaterButton");
        _presentation = new ApplicationUpdatePresentation(version);

        Title = DesktopUiStrings.UpdateTitle;
        _updateButton.Content = DesktopUiStrings.UpdateInstall;
        _laterButton.Content = DesktopUiStrings.UpdateLater;
        ApplyPresentation();

        _updateButton.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _laterButton.Click += (_, _) => LaterRequested?.Invoke(this, EventArgs.Empty);
        Closing += OnClosing;
    }

    public void ShowDownloadProgress(int progress)
    {
        _presentation.ShowDownloadProgress(progress);
        ApplyPresentation();
    }

    public void ShowInstalling()
    {
        _presentation.ShowInstalling();
        ApplyPresentation();
    }

    public void ShowInstallFailure()
    {
        _presentation.ShowInstallFailure();
        ApplyPresentation();
    }

    public void CloseForShutdown()
    {
        _isShutdownCloseRequested = true;
        Close();
    }

    private TControl GetRequiredControl<TControl>(string name)
        where TControl : Control
    {
        return this.FindControl<TControl>(name)
            ?? throw new InvalidOperationException(
                $"Required update window control '{name}' was not found.");
    }

    private void ApplyPresentation()
    {
        _messageText.Text = _presentation.Message;
        _progressBar.IsVisible = _presentation.IsProgressVisible;
        _progressBar.IsIndeterminate = _presentation.IsProgressIndeterminate;
        _progressBar.Value = _presentation.DownloadProgress;
        _updateButton.IsEnabled = _presentation.AreActionsEnabled;
        _laterButton.IsEnabled = _presentation.AreActionsEnabled;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _ = sender;
        e.Cancel = _presentation.IsBusy && !_isShutdownCloseRequested;
    }
}

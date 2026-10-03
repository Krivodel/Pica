using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

using Pica.Desktop.ViewModels;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;

namespace Pica.Desktop.Views;

internal sealed partial class ClipboardShortcutOfferWindow : SukiWindow
{
    internal Task SettingsCompletion => _settings.Completion;

    private readonly ClipboardShortcutOfferViewModel _viewModel;
    private readonly ViewerSettingsContentControl _settings;

    internal ClipboardShortcutOfferWindow(ClipboardShortcutOfferViewModel viewModel,
        IReadOnlyList<ViewerSettingContribution> settings)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        ArgumentNullException.ThrowIfNull(settings);
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        _settings = new ViewerSettingsContentControl(settings);
        ContentControl host = this.FindControl<ContentControl>("SettingsHost")
            ?? throw new InvalidOperationException("The clipboard settings host was not created.");
        host.Content = _settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        viewModel.CloseRequested += OnCloseRequested;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _viewModel.EnableCommand.Cancel();
        _viewModel.DeclineCommand.Cancel();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnSettingsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ViewerSettingsContentControl.IsRecordingProperty)
        {
            _viewModel.IsRecording = _settings.IsRecording;
        }
    }
}

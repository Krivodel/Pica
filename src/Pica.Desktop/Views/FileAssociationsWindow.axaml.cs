using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.ViewModels;

namespace Pica.Desktop.Views;

internal sealed partial class FileAssociationsWindow : SukiWindow
{
    private readonly FileAssociationsViewModel _viewModel;

    internal FileAssociationsWindow(FileAssociationsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        LocalizationText.Instance.PropertyChanged += OnLocalizationChanged;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _viewModel.CancelPendingOperations();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        LocalizationText.Instance.PropertyChanged -= OnLocalizationChanged;
        base.OnClosed(e);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        _viewModel.RefreshLocalization();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}

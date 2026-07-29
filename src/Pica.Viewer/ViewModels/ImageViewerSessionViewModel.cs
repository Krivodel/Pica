using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed partial class ImageViewerSessionViewModel :
    ObservableObject,
    IDisposable
{
    internal IReadOnlyList<PicaImageItem> Items => _session.Items;
    internal IReadOnlyList<PicaActionDefinition> Actions => _session.Actions;
    internal PicaImageItem? SelectedItem => _session.SelectedItem;
    internal int SelectedIndex => _session.SelectedIndex;
    internal int PreferredNavigationDirection =>
        _session.PreferredNavigationDirection;
    internal bool IsChannelModeActive => _session.IsChannelModeActive;
    internal bool IsMainImageModeActive => _session.IsMainImageModeActive;
    internal bool IsChannelAvailabilityKnown =>
        _session.IsChannelAvailabilityKnown;
    internal ImageChannel? SelectedChannel => _session.SelectedChannel;
    internal bool IsFilteringEnabled => _session.IsFilteringEnabled;

    private readonly ImageViewerSession _session;

    internal ImageViewerSessionViewModel(ImageViewerSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public void Dispose()
    {
        _session.PropertyChanged -= OnSessionPropertyChanged;
    }

    [RelayCommand]
    private void ToggleImageMode()
    {
        _session.ToggleImageMode();
    }

    [RelayCommand]
    private void SelectMainImageMode()
    {
        _session.SelectMainImageMode();
    }

    [RelayCommand]
    private void SelectChannelImageMode()
    {
        _session.SelectChannelImageMode();
    }

    [RelayCommand]
    private void Navigate(int direction)
    {
        _session.Navigate(direction);
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.PropertyName is { } propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed partial class ImageViewerSession : ObservableObject
{
    internal IReadOnlyList<PicaImageItem> Items { get; }
    internal IReadOnlyList<PicaActionDefinition> Actions { get; }
    internal PicaImageItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count
            ? Items[SelectedIndex]
            : null;
    internal bool IsChannelModeActive => ImageMode == ViewerImageMode.Channels;
    internal bool IsMainImageModeActive => ImageMode == ViewerImageMode.Main;
    internal bool IsChannelAvailabilityKnown { get; private set; }

    private IReadOnlyList<ImageChannel> _availableChannels;
    private int _selectedChannelIndex;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedItem))]
    private int _selectedIndex;
    [ObservableProperty]
    private int _preferredNavigationDirection = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChannelModeActive))]
    [NotifyPropertyChangedFor(nameof(IsMainImageModeActive))]
    private ViewerImageMode _imageMode;
    [ObservableProperty]
    private ImageChannel? _selectedChannel;
    [ObservableProperty]
    private bool _isFilteringEnabled;

    internal ImageViewerSession(
        PicaViewerRequest request,
        bool isFilteringEnabled)
    {
        ArgumentNullException.ThrowIfNull(request);
        Items = request.Items;
        Actions = request.Actions;
        _availableChannels = ImageChannel.ColorChannels;
        _selectedIndex = GetItemIndexOrDefault(
            request.Items,
            request.SelectedItemId);
        _isFilteringEnabled = isFilteringEnabled;
    }

    internal void ToggleImageMode()
    {
        if (IsChannelModeActive)
        {
            SelectMainImageMode();
            return;
        }

        SelectChannelImageMode();
    }

    internal void SelectMainImageMode()
    {
        ImageMode = ViewerImageMode.Main;
        SelectedChannel = null;
    }

    internal void SelectChannelImageMode()
    {
        if (IsChannelModeActive)
        {
            return;
        }

        _availableChannels = ImageChannel.ColorChannels;
        _selectedChannelIndex = 0;
        IsChannelAvailabilityKnown = false;
        SelectedChannel = _availableChannels[_selectedChannelIndex];
        ImageMode = ViewerImageMode.Channels;
    }

    internal void Navigate(int direction)
    {
        if (direction == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "The navigation direction must not be zero.");
        }

        if (IsChannelModeActive)
        {
            NavigateChannel(direction);
            return;
        }

        NavigateImage(direction);
    }

    internal void ToggleFiltering()
    {
        IsFilteringEnabled = !IsFilteringEnabled;
    }

    internal void SetHasAlpha(bool hasAlpha)
    {
        _availableChannels = hasAlpha
            ? ImageChannel.ColorAndAlphaChannels
            : ImageChannel.ColorChannels;
        _selectedChannelIndex = Math.Clamp(
            _selectedChannelIndex,
            0,
            _availableChannels.Count - 1);
        SelectedChannel = _availableChannels[_selectedChannelIndex];
        IsChannelAvailabilityKnown = true;
    }

    private static int GetItemIndexOrDefault(
        IReadOnlyList<PicaImageItem> items,
        Guid itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id == itemId)
            {
                return i;
            }
        }

        return items.Count == 0 ? -1 : 0;
    }

    private void NavigateChannel(int direction)
    {
        int step = direction < 0 ? -1 : 1;
        _selectedChannelIndex = (
            _selectedChannelIndex
            + step
            + _availableChannels.Count)
            % _availableChannels.Count;
        SelectedChannel = _availableChannels[_selectedChannelIndex];
    }

    private void NavigateImage(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        int currentIndex = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
        PreferredNavigationDirection = direction < 0 ? -1 : 1;
        SelectedIndex = (currentIndex + direction + Items.Count) % Items.Count;
    }
}

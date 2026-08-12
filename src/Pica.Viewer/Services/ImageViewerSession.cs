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
    internal int FrameCount => _frameCount;
    internal int SelectedFrameIndex => _selectedFrameIndex;
    internal ImageFramePresentationModes FramePresentationMode =>
        _framePresentationMode;
    internal ImageFrameNumbering FrameNumbering =>
        _frameNumbering;
    internal bool CanNavigateFrames =>
        (SelectedContentGroupKind
            == ImageContentGroupKind.Animation)
        && !IsContentGroupLoading
        && (_frameCount > 1);
    internal bool IsAnimationPlaybackEnabled =>
        ((_framePresentationMode
                & ImageFramePresentationModes.AutomaticPlayback)
            != ImageFramePresentationModes.None)
        && (_frameCount > 1);
    internal IReadOnlyList<ImageContentGroupDefinition> ContentGroups =>
        _contentGroups;
    internal int SelectedContentGroupIndex =>
        _selectedContentGroupIndex;
    internal bool HasMixedContent =>
        HasStillImagesContent && HasAnimationContent;
    internal bool HasStillImagesContent =>
        GetContentGroupCount(
            ImageContentGroupKind.StillImages) > 0;
    internal bool HasAnimationContent =>
        GetContentGroupCount(
            ImageContentGroupKind.Animation) > 0;
    internal ImageContentGroupKind? SelectedContentGroupKind =>
        (_selectedContentGroupIndex >= 0)
        && (_selectedContentGroupIndex < _contentGroups.Count)
            ? _contentGroups[_selectedContentGroupIndex].Kind
            : null;
    internal ImageContentNavigationState ContentNavigationState =>
        _contentNavigationState;

    internal event EventHandler<ImageContentNavigationRequestedEventArgs>?
        ContentNavigationRequested;

    private IReadOnlyList<ImageChannel> _availableChannels;
    private int _selectedChannelIndex;
    private int _frameCount;
    private int _selectedFrameIndex;
    private ImageFramePresentationModes _framePresentationMode;
    private ImageFrameNumbering _frameNumbering;
    private IReadOnlyList<ImageContentGroupDefinition> _contentGroups = [];
    private readonly Dictionary<int, int> _contentGroupItemIndices = [];
    private ImageContentNavigationState _contentNavigationState =
        ImageContentNavigationState.Empty;
    private int _selectedContentGroupIndex = -1;
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
    [ObservableProperty]
    private bool _isAnimationBuffering;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigateFrames))]
    private bool _isContentGroupLoading;

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
        ValidateNavigationDirection(direction);

        if (IsChannelModeActive)
        {
            NavigateChannel(direction);
            return;
        }

        NavigateImage(direction);
    }

    internal void NavigateFrame(int direction)
    {
        ValidateNavigationDirection(direction);

        if (!CanNavigateFrames)
        {
            return;
        }

        int frameDirection = direction;

        if (_frameNumbering == ImageFrameNumbering.Reverse)
        {
            frameDirection = direction < 0 ? 1 : -1;
        }

        int frameIndex = GetAdjacentFrameIndex(frameDirection);
        SetSelectedFrameIndex(frameIndex);
    }

    internal void NavigateContent(int direction)
    {
        ValidateNavigationDirection(direction);

        if (!IsContentGroupIndexValid(_selectedContentGroupIndex))
        {
            return;
        }

        int contentUnitCount = GetContentUnitCount();

        if (contentUnitCount <= 1)
        {
            return;
        }

        int step = direction < 0 ? -1 : 1;
        int currentUnitIndex = GetSelectedContentUnitIndex();
        int targetUnitIndex = (
            currentUnitIndex
            + step
            + contentUnitCount)
            % contentUnitCount;
        (int groupIndex, int itemIndex) =
            GetContentNavigationTarget(targetUnitIndex);
        ImageContentGroupDefinition targetGroup =
            _contentGroups[groupIndex];
        int frameIndex = GetFrameIndex(targetGroup, itemIndex);
        _contentGroupItemIndices[groupIndex] = itemIndex;

        ContentNavigationRequested?.Invoke(
            this,
            new ImageContentNavigationRequestedEventArgs(
                groupIndex,
                frameIndex));

        if (groupIndex == _selectedContentGroupIndex)
        {
            SetSelectedFrameIndex(frameIndex);
            return;
        }

        SelectContentGroup(groupIndex);
    }

    internal void AdvanceAnimationFrame()
    {
        if (!IsAnimationPlaybackEnabled)
        {
            return;
        }

        SetSelectedFrameIndex(GetAdjacentFrameIndex(1));
    }

    internal void SetAnimationBuffering(bool isAnimationBuffering)
    {
        IsAnimationBuffering = isAnimationBuffering;
    }

    internal void SetContentGroups(
        IReadOnlyList<ImageContentGroupDefinition> contentGroups,
        int selectedContentGroupIndex)
    {
        ArgumentNullException.ThrowIfNull(contentGroups);

        if ((selectedContentGroupIndex < 0)
            || (selectedContentGroupIndex >= contentGroups.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedContentGroupIndex),
                selectedContentGroupIndex,
                $"The selected content group index must be between 0 and {contentGroups.Count - 1}.");
        }

        _contentGroups = contentGroups;
        _contentGroupItemIndices.Clear();

        for (int groupIndex = 0;
            groupIndex < contentGroups.Count;
            groupIndex++)
        {
            _contentGroupItemIndices[groupIndex] = 0;
        }

        SetSelectedContentGroupIndex(selectedContentGroupIndex);
        OnContentGroupsChanged();
    }

    internal void ClearContentGroups()
    {
        IsContentGroupLoading = false;
        _contentGroups = [];
        _contentGroupItemIndices.Clear();
        SetSelectedContentGroupIndex(-1);
        OnContentGroupsChanged();
    }

    internal void SelectContentGroup(int groupIndex)
    {
        if ((groupIndex < 0)
            || (groupIndex >= _contentGroups.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(groupIndex),
                groupIndex,
                $"The content group index must be between 0 and {_contentGroups.Count - 1}.");
        }

        SetSelectedContentGroupIndex(groupIndex);
    }

    internal void RestoreContentGroupSelection(int groupIndex)
    {
        if ((groupIndex < 0)
            || (groupIndex >= _contentGroups.Count))
        {
            return;
        }

        SelectContentGroup(groupIndex);
    }

    internal void SetContentGroupLoading(bool isLoading)
    {
        IsContentGroupLoading = isLoading;
        UpdateContentNavigationState();
    }

    internal void SetFramePresentation(
        int frameCount,
        ImageFramePresentationModes framePresentationMode,
        int preferredInitialFrameIndex,
        ImageFrameNumbering frameNumbering =
            ImageFrameNumbering.Forward)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount),
                frameCount,
                "The frame count must be positive.");
        }

        if ((preferredInitialFrameIndex < 0)
            || (preferredInitialFrameIndex >= frameCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredInitialFrameIndex),
                preferredInitialFrameIndex,
                $"The preferred frame index must be between 0 and {frameCount - 1}.");
        }

        ApplyFramePresentation(
            frameCount,
            framePresentationMode,
            preferredInitialFrameIndex,
            frameNumbering);
        StoreSelectedContentGroupItemIndex(
            preferredInitialFrameIndex);
        UpdateContentNavigationState();
    }

    internal void ClearFramePresentation()
    {
        IsAnimationBuffering = false;
        ApplyFramePresentation(
            0,
            ImageFramePresentationModes.None,
            0,
            ImageFrameNumbering.Forward);
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

    private static void ValidateNavigationDirection(int direction)
    {
        if (direction == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "The navigation direction must not be zero.");
        }
    }

    private static int GetContentGroupUnitCount(
        ImageContentGroupDefinition group)
    {
        return group.Kind == ImageContentGroupKind.StillImages
            ? group.ItemCount
            : 1;
    }

    private static int GetFrameIndex(
        ImageContentGroupDefinition group,
        int itemIndex)
    {
        return group.FrameNumbering == ImageFrameNumbering.Reverse
            ? group.ItemCount - itemIndex - 1
            : itemIndex;
    }

    private static int GetItemIndex(
        ImageContentGroupDefinition group,
        int frameIndex)
    {
        int clampedFrameIndex = Math.Clamp(
            frameIndex,
            0,
            group.ItemCount - 1);

        return group.FrameNumbering == ImageFrameNumbering.Reverse
            ? group.ItemCount - clampedFrameIndex - 1
            : clampedFrameIndex;
    }

    private int GetContentGroupCount(ImageContentGroupKind kind)
    {
        int count = 0;

        foreach (ImageContentGroupDefinition group in _contentGroups)
        {
            if (group.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private int GetStillImageCount()
    {
        return _contentGroups
            .Where(group =>
                group.Kind == ImageContentGroupKind.StillImages)
            .Sum(GetContentGroupUnitCount);
    }

    private int GetContentUnitCount()
    {
        return _contentGroups.Sum(GetContentGroupUnitCount);
    }

    private int GetSelectedContentUnitIndex()
    {
        int unitIndex = 0;

        for (int groupIndex = 0;
            groupIndex < _contentGroups.Count;
            groupIndex++)
        {
            ImageContentGroupDefinition group =
                _contentGroups[groupIndex];

            if (groupIndex == _selectedContentGroupIndex)
            {
                int itemIndex = _contentGroupItemIndices.GetValueOrDefault(
                    groupIndex,
                    GetItemIndex(group, _selectedFrameIndex));

                return unitIndex + itemIndex;
            }

            unitIndex += GetContentGroupUnitCount(group);
        }

        throw new InvalidOperationException(
            "The selected image content group is unavailable.");
    }

    private (int GroupIndex, int ItemIndex) GetContentNavigationTarget(
        int targetUnitIndex)
    {
        int remainingIndex = targetUnitIndex;

        for (int groupIndex = 0;
            groupIndex < _contentGroups.Count;
            groupIndex++)
        {
            int groupUnitCount = GetContentGroupUnitCount(
                _contentGroups[groupIndex]);

            if (remainingIndex < groupUnitCount)
            {
                return (groupIndex, remainingIndex);
            }

            remainingIndex -= groupUnitCount;
        }

        throw new ArgumentOutOfRangeException(
            nameof(targetUnitIndex),
            targetUnitIndex,
            "The image content navigation target is unavailable.");
    }

    private bool IsContentGroupIndexValid(int groupIndex)
    {
        return (groupIndex >= 0)
            && (groupIndex < _contentGroups.Count);
    }

    private void StoreSelectedContentGroupItemIndex(int frameIndex)
    {
        if (!IsContentGroupIndexValid(_selectedContentGroupIndex))
        {
            return;
        }

        ImageContentGroupDefinition group =
            _contentGroups[_selectedContentGroupIndex];

        if (group.Kind == ImageContentGroupKind.StillImages)
        {
            _contentGroupItemIndices[_selectedContentGroupIndex] =
                GetItemIndex(group, frameIndex);
        }
    }

    private void SetSelectedContentGroupIndex(int groupIndex)
    {
        if (SetProperty(
            ref _selectedContentGroupIndex,
            groupIndex,
            nameof(SelectedContentGroupIndex)))
        {
            OnPropertyChanged(nameof(SelectedContentGroupKind));
            OnPropertyChanged(nameof(CanNavigateFrames));
        }

        UpdateContentNavigationState();
    }

    private void OnContentGroupsChanged()
    {
        OnPropertyChanged(nameof(ContentGroups));
        OnPropertyChanged(nameof(HasMixedContent));
        OnPropertyChanged(nameof(HasStillImagesContent));
        OnPropertyChanged(nameof(HasAnimationContent));
        OnPropertyChanged(nameof(SelectedContentGroupKind));
        OnPropertyChanged(nameof(CanNavigateFrames));
        UpdateContentNavigationState();
    }

    private void ApplyFramePresentation(
        int frameCount,
        ImageFramePresentationModes framePresentationMode,
        int preferredInitialFrameIndex,
        ImageFrameNumbering frameNumbering)
    {
        ImageFramePresentationModes effectiveMode = frameCount > 1
            ? framePresentationMode
            : ImageFramePresentationModes.None;

        if (!effectiveMode.HasFlag(
            ImageFramePresentationModes.AutomaticPlayback))
        {
            IsAnimationBuffering = false;
        }

        bool modeChanged = SetProperty(
            ref _framePresentationMode,
            effectiveMode,
            nameof(FramePresentationMode));
        bool frameCountChanged = SetProperty(
            ref _frameCount,
            frameCount,
            nameof(FrameCount));
        SetProperty(
            ref _frameNumbering,
            frameNumbering,
            nameof(FrameNumbering));
        SetSelectedFrameIndex(preferredInitialFrameIndex);

        if (frameCountChanged)
        {
            OnPropertyChanged(nameof(CanNavigateFrames));
        }

        if (modeChanged || frameCountChanged)
        {
            OnPropertyChanged(nameof(IsAnimationPlaybackEnabled));
        }
    }

    private int GetAdjacentFrameIndex(int direction)
    {
        int step = direction < 0 ? -1 : 1;

        return (
            _selectedFrameIndex
            + step
            + _frameCount)
            % _frameCount;
    }

    private void SetSelectedFrameIndex(int frameIndex)
    {
        SetProperty(
            ref _selectedFrameIndex,
            frameIndex,
            nameof(SelectedFrameIndex));
        UpdateContentNavigationState();
    }

    private void UpdateContentNavigationState()
    {
        int imageCount = GetStillImageCount();
        int animationCount = GetContentGroupCount(
            ImageContentGroupKind.Animation);
        ImageContentGroupKind? selectedKind =
            SelectedContentGroupKind;
        int selectedContentCount = selectedKind switch
        {
            ImageContentGroupKind.StillImages => imageCount,
            ImageContentGroupKind.Animation => animationCount,
            _ => 0
        };
        int selectedContentNumber = selectedKind is null
            ? 0
            : GetSelectedContentNumber(selectedKind.Value);
        int selectedFrameNumber =
            selectedKind == ImageContentGroupKind.Animation
            && (_frameCount > 0)
                ? GetDisplayedFrameNumber()
                : 0;
        ImageContentNavigationState state = new(
            imageCount,
            animationCount,
            selectedKind,
            selectedContentNumber,
            selectedContentCount,
            selectedFrameNumber,
            _frameCount,
            IsContentGroupIndexValid(_selectedContentGroupIndex)
                && (GetContentUnitCount() > 1),
            CanNavigateFrames);
        SetProperty(
            ref _contentNavigationState,
            state,
            nameof(ContentNavigationState));
    }

    private int GetSelectedContentNumber(
        ImageContentGroupKind selectedKind)
    {
        int contentNumber = 0;

        for (int groupIndex = 0;
            groupIndex < _contentGroups.Count;
            groupIndex++)
        {
            ImageContentGroupDefinition group =
                _contentGroups[groupIndex];

            if (group.Kind != selectedKind)
            {
                continue;
            }

            if (groupIndex == _selectedContentGroupIndex)
            {
                if (selectedKind == ImageContentGroupKind.Animation)
                {
                    return contentNumber + 1;
                }

                int itemIndex = _contentGroupItemIndices.GetValueOrDefault(
                    groupIndex,
                    GetItemIndex(group, _selectedFrameIndex));

                return contentNumber + itemIndex + 1;
            }

            contentNumber += GetContentGroupUnitCount(group);
        }

        return 0;
    }

    private int GetDisplayedFrameNumber()
    {
        return _frameNumbering == ImageFrameNumbering.Reverse
            ? _frameCount - _selectedFrameIndex
            : _selectedFrameIndex + 1;
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

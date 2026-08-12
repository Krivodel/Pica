using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Pica.Protocol;
using Pica.Viewer.Helpers;
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
    internal int FrameCount => _session.FrameCount;
    internal int SelectedFrameIndex => _session.SelectedFrameIndex;
    internal bool CanNavigateFrames => _session.CanNavigateFrames;
    internal ImageContentGroupKind? SelectedContentGroupKind =>
        _session.SelectedContentGroupKind;
    internal bool IsAnimationPlaybackEnabled =>
        _session.IsAnimationPlaybackEnabled;
    internal bool CanControlAnimationPlayback =>
        _session.CanControlAnimationPlayback;
    internal bool IsAnimationPlaybackActive =>
        _session.IsAnimationPlaybackActive;
    internal bool IsAnimationBuffering =>
        _session.IsAnimationBuffering;
    internal bool IsContentGroupLoading =>
        _session.IsContentGroupLoading;
    internal bool IsImageLoading =>
        IsAnimationBuffering || IsContentGroupLoading;
    internal bool IsContentNavigationPanelVisible =>
        IsImagesOnlyNavigationVisible || IsAnimationNavigationVisible;
    internal bool IsNavigationInformationVisible =>
        IsContentSelectionVisible || IsAnimationTimeVisible;
    internal bool IsImagesOnlyNavigationVisible =>
        (ContentNavigationState.AnimationCount == 0)
        && ContentNavigationState.CanNavigateContent;
    internal bool IsAnimationNavigationVisible =>
        ContentNavigationState.AnimationCount > 0;
    internal bool IsContentSelectionVisible =>
        ContentNavigationState.CanNavigateContent;
    internal bool IsContentNavigationEnabled =>
        ContentNavigationState.CanNavigateContent;
    internal bool IsFrameSelectionVisible =>
        ContentNavigationState.CanNavigateFrames;
    internal bool IsAnimationTimeVisible =>
        IsFrameSelectionVisible;
    internal bool IsAnimationTimelineEnabled =>
        ContentNavigationState.CanNavigateFrames;
    internal bool IsPlayAnimationIconVisible =>
        !IsPauseAnimationIconVisible;
    internal bool IsPauseAnimationIconVisible =>
        CanControlAnimationPlayback
        && IsAnimationPlaybackActive;
    internal double AnimationTimelineMaximum =>
        IsAnimationTimelineEnabled
            ? Math.Max(
                1d,
                FrameCount - 1d)
            : 1d;
    internal double AnimationTimelineValue =>
        IsAnimationTimelineEnabled
            ? SelectedFrameIndex
            : 0d;
    internal bool IsAnimationTimeSeparatorVisible =>
        IsContentSelectionVisible && IsAnimationTimeVisible;
    internal string SelectedContentText =>
        IsContentSelectionVisible
        && ContentNavigationState.SelectedKind is { } selectedKind
            ? ImageContentNavigationFormatter.FormatContent(
                selectedKind,
                ContentNavigationState.SelectedContentNumber,
                ContentNavigationState.SelectedContentCount,
                ContentNavigationState.ImageCount,
                ContentNavigationState.AnimationCount)
            : string.Empty;
    internal string AnimationTimeText =>
        IsAnimationTimeVisible
            ? ImageContentNavigationFormatter.FormatAnimationTime(
                _session.AnimationPosition,
                _session.AnimationDuration)
            : string.Empty;
    internal string ImageContentWidthReferenceText =>
        IsContentSelectionVisible
        && (ContentNavigationState.ImageCount > 0)
            ? ImageContentNavigationFormatter
                .FormatContentWidthReference(
                    ImageContentGroupKind.StillImages,
                    ContentNavigationState.ImageCount,
                    ContentNavigationState.ImageCount,
                    ContentNavigationState.AnimationCount)
            : string.Empty;
    internal string AnimationContentWidthReferenceText =>
        IsContentSelectionVisible
        && (ContentNavigationState.AnimationCount > 0)
            ? ImageContentNavigationFormatter
                .FormatContentWidthReference(
                    ImageContentGroupKind.Animation,
                    ContentNavigationState.AnimationCount,
                    ContentNavigationState.ImageCount,
                    ContentNavigationState.AnimationCount)
            : string.Empty;
    internal string AnimationTimeWidthReferenceText =>
        IsAnimationTimeVisible
            ? ImageContentNavigationFormatter
                .FormatAnimationTimeWidthReference(
                    _session.AnimationDuration)
            : string.Empty;

    private ImageContentNavigationState ContentNavigationState =>
        _session.ContentNavigationState;

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

    [RelayCommand]
    private void NavigateFrame(int direction)
    {
        _session.NavigateFrame(direction);
    }

    [RelayCommand]
    private void NavigateContent(int direction)
    {
        _session.NavigateContent(direction);
    }

    [RelayCommand(CanExecute = nameof(CanToggleAnimationPlayback))]
    private void ToggleAnimationPlayback()
    {
        _session.ToggleAnimationPlayback();
    }

    [RelayCommand(CanExecute = nameof(CanSeekAnimation))]
    private void SeekAnimation(double framePosition)
    {
        double clampedFramePosition = Math.Clamp(
            framePosition,
            0d,
            FrameCount - 1d);
        int frameIndex = (int)Math.Round(
            clampedFramePosition,
            MidpointRounding.AwayFromZero);

        if (frameIndex == SelectedFrameIndex)
        {
            return;
        }

        _session.SeekAnimationFrame(frameIndex);
    }

    private bool CanToggleAnimationPlayback()
    {
        return CanControlAnimationPlayback;
    }

    private bool CanSeekAnimation()
    {
        return IsAnimationTimelineEnabled;
    }

    private void OnSessionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.PropertyName is { } propertyName)
        {
            OnPropertyChanged(propertyName);

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.IsAnimationBuffering),
                StringComparison.Ordinal)
                || string.Equals(
                    propertyName,
                    nameof(ImageViewerSession.IsContentGroupLoading),
                    StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(IsImageLoading));
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.ContentNavigationState),
                StringComparison.Ordinal))
            {
                OnContentNavigationStateChanged();
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.CanControlAnimationPlayback),
                StringComparison.Ordinal))
            {
                ToggleAnimationPlaybackCommand
                    .NotifyCanExecuteChanged();
                OnAnimationPlaybackStateChanged();
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.IsAnimationPlaybackActive),
                StringComparison.Ordinal))
            {
                OnAnimationPlaybackStateChanged();
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.AnimationPosition),
                StringComparison.Ordinal))
            {
                OnAnimationPositionChanged();
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.SelectedFrameIndex),
                StringComparison.Ordinal))
            {
                OnSelectedFrameIndexChanged();
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.FrameCount),
                StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(AnimationTimelineMaximum));
            }

            if (string.Equals(
                propertyName,
                nameof(ImageViewerSession.AnimationDuration),
                StringComparison.Ordinal))
            {
                OnAnimationDurationChanged();
            }
        }
    }

    private void OnContentNavigationStateChanged()
    {
        OnPropertyChanged(nameof(IsContentNavigationPanelVisible));
        OnPropertyChanged(nameof(IsNavigationInformationVisible));
        OnPropertyChanged(nameof(IsImagesOnlyNavigationVisible));
        OnPropertyChanged(nameof(IsAnimationNavigationVisible));
        OnPropertyChanged(nameof(IsContentSelectionVisible));
        OnPropertyChanged(nameof(IsContentNavigationEnabled));
        OnPropertyChanged(nameof(IsFrameSelectionVisible));
        OnPropertyChanged(nameof(IsAnimationTimeVisible));
        OnPropertyChanged(nameof(IsAnimationTimelineEnabled));
        OnPropertyChanged(nameof(AnimationTimelineMaximum));
        OnPropertyChanged(nameof(AnimationTimelineValue));
        OnPropertyChanged(nameof(IsAnimationTimeSeparatorVisible));
        OnPropertyChanged(nameof(SelectedContentText));
        OnPropertyChanged(nameof(AnimationTimeText));
        OnPropertyChanged(nameof(ImageContentWidthReferenceText));
        OnPropertyChanged(nameof(AnimationContentWidthReferenceText));
        OnPropertyChanged(nameof(AnimationTimeWidthReferenceText));
        SeekAnimationCommand.NotifyCanExecuteChanged();
        OnAnimationPlaybackStateChanged();
    }

    private void OnAnimationPlaybackStateChanged()
    {
        OnPropertyChanged(nameof(CanControlAnimationPlayback));
        OnPropertyChanged(nameof(IsAnimationPlaybackActive));
        OnPropertyChanged(nameof(IsPlayAnimationIconVisible));
        OnPropertyChanged(nameof(IsPauseAnimationIconVisible));
    }

    private void OnAnimationPositionChanged()
    {
        OnPropertyChanged(nameof(AnimationTimeText));
    }

    private void OnSelectedFrameIndexChanged()
    {
        OnPropertyChanged(nameof(AnimationTimelineValue));
    }

    private void OnAnimationDurationChanged()
    {
        OnPropertyChanged(nameof(AnimationTimeText));
        OnPropertyChanged(nameof(AnimationTimeWidthReferenceText));
    }
}

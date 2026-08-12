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
    internal bool IsAnimationBuffering =>
        _session.IsAnimationBuffering;
    internal bool IsContentGroupLoading =>
        _session.IsContentGroupLoading;
    internal bool IsImageLoading =>
        IsAnimationBuffering || IsContentGroupLoading;
    internal bool IsContentNavigationPanelVisible =>
        IsImagesOnlyNavigationVisible || IsAnimationNavigationVisible;
    internal bool IsNavigationInformationVisible =>
        IsContentSelectionVisible || IsFrameSelectionVisible;
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
    internal bool IsFrameSelectionSeparatorVisible =>
        IsContentSelectionVisible && IsFrameSelectionVisible;
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
    internal string SelectedFrameText =>
        IsFrameSelectionVisible
            ? ImageContentNavigationFormatter.FormatFrame(
                ContentNavigationState.SelectedFrameNumber,
                ContentNavigationState.FrameCount)
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
    internal string FrameWidthReferenceText =>
        IsFrameSelectionVisible
            ? ImageContentNavigationFormatter
                .FormatFrameWidthReference(
                    ContentNavigationState.FrameCount)
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
        OnPropertyChanged(nameof(IsFrameSelectionSeparatorVisible));
        OnPropertyChanged(nameof(SelectedContentText));
        OnPropertyChanged(nameof(SelectedFrameText));
        OnPropertyChanged(nameof(ImageContentWidthReferenceText));
        OnPropertyChanged(nameof(AnimationContentWidthReferenceText));
        OnPropertyChanged(nameof(FrameWidthReferenceText));
    }
}

using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerSessionViewModelTests
{
    private static readonly Guid FirstItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_WithSelectedItem_ExposesSelectedItem()
    {
        PicaViewerRequest request = CreateRequest(SecondItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, true));

        viewModel.SelectedItem.Should().Be(request.Items[1]);
        viewModel.SelectedIndex.Should().Be(1);
        viewModel.IsFilteringEnabled.Should().BeTrue();
        viewModel.IsMainImageModeActive.Should().BeTrue();
    }

    [Fact]
    public void SelectChannelImageModeCommand_WhenMainMode_SelectsRedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.IsChannelModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().Be(ImageChannel.Red);
        viewModel.IsChannelAvailabilityKnown.Should().BeFalse();
    }

    [Fact]
    public void ToggleImageModeCommand_WhenChannelMode_ReturnsToMainMode()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.ToggleImageModeCommand.Execute(null);

        viewModel.IsMainImageModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().BeNull();
    }

    [Fact]
    public void ToggleImageModeCommand_WhenMainMode_SelectsRedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.ToggleImageModeCommand.Execute(null);

        viewModel.IsChannelModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().Be(ImageChannel.Red);
    }

    [Fact]
    public void NavigateCommand_WithMainMode_NavigatesImagesAndRecordsDirection()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.NavigateCommand.Execute(1);

        viewModel.SelectedIndex.Should().Be(1);
        viewModel.SelectedItem?.Id.Should().Be(SecondItemId);
        viewModel.PreferredNavigationDirection.Should().Be(1);
    }

    [Fact]
    public void NavigateCommand_WithColorChannels_WrapsFromRedToBlue()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.NavigateCommand.Execute(-1);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Blue);
        viewModel.SelectedIndex.Should().Be(0);
        viewModel.PreferredNavigationDirection.Should().Be(1);
    }

    [Fact]
    public void NavigateCommand_WithAlphaChannel_WrapsFromRedToAlpha()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        viewModel.SelectChannelImageModeCommand.Execute(null);
        session.SetHasAlpha(true);

        viewModel.NavigateCommand.Execute(-1);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Alpha);
        viewModel.IsChannelAvailabilityKnown.Should().BeTrue();
    }

    [Fact]
    public void NavigateCommand_WithZeroDirection_ThrowsArgumentOutOfRangeException()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        Action act = () => viewModel.NavigateCommand.Execute(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NavigateFrameCommand_WithAnimationFrames_WrapsBackward()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0);

        viewModel.NavigateFrameCommand.Execute(-1);

        viewModel.SelectedFrameIndex.Should().Be(2);
        viewModel.FrameCount.Should().Be(3);
        viewModel.CanNavigateFrames.Should().BeTrue();
    }

    [Fact]
    public void NavigateFrameCommand_WithAnimationFrames_AdvancesForward()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    4)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);

        viewModel.NavigateFrameCommand.Execute(1);

        viewModel.SelectedFrameIndex.Should().Be(1);
    }

    [Fact]
    public void NavigateFrameCommand_WithStillImages_KeepsSelection()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    4,
                    ImageFrameNumbering.Reverse)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.ManualNavigation,
            3,
            ImageFrameNumbering.Reverse);

        viewModel.NavigateFrameCommand.Execute(1);

        viewModel.SelectedFrameIndex.Should().Be(3);
        viewModel.CanNavigateFrames.Should().BeFalse();
    }

    [Fact]
    public void NavigateFrameCommand_WhileAnimationContentLoads_KeepsSelection()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        session.SetContentGroupLoading(true);

        viewModel.NavigateFrameCommand.Execute(1);

        viewModel.SelectedFrameIndex.Should().Be(0);
        viewModel.CanNavigateFrames.Should().BeFalse();
    }

    [Fact]
    public void NavigateContentCommand_WithMixedContent_CyclesImagesAndAnimations()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        IReadOnlyList<ImageContentGroupDefinition> groups =
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    2),
                new(
                    ImageContentGroupKind.Animation,
                    12),
                new(
                    ImageContentGroupKind.Animation,
                    24)
            }.AsReadOnly();
        List<(int GroupIndex, int FrameIndex)> transitions = [];
        session.ContentNavigationRequested += (_, e) =>
            transitions.Add((e.GroupIndex, e.FrameIndex));
        session.SetContentGroups(groups, 0);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.ManualNavigation,
            0);

        viewModel.NavigateContentCommand.Execute(1);
        viewModel.SelectedFrameIndex.Should().Be(1);
        session.SelectedContentGroupIndex.Should().Be(0);

        viewModel.NavigateContentCommand.Execute(1);
        session.SelectedContentGroupIndex.Should().Be(1);

        viewModel.NavigateContentCommand.Execute(1);
        session.SelectedContentGroupIndex.Should().Be(2);

        viewModel.NavigateContentCommand.Execute(1);
        session.SelectedContentGroupIndex.Should().Be(0);

        viewModel.NavigateContentCommand.Execute(1);

        viewModel.SelectedFrameIndex.Should().Be(1);
        transitions.Should().Equal(
            (0, 1),
            (1, 0),
            (2, 0),
            (0, 0),
            (0, 1));
    }

    [Fact]
    public void NavigateContentCommand_WithReverseImageNumbering_UsesDisplayedOrder()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        IReadOnlyList<ImageContentGroupDefinition> groups =
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    3,
                    ImageFrameNumbering.Reverse)
            }.AsReadOnly();
        session.SetContentGroups(groups, 0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.ManualNavigation,
            2,
            ImageFrameNumbering.Reverse);

        viewModel.NavigateContentCommand.Execute(1);

        viewModel.SelectedFrameIndex.Should().Be(1);
    }

    [Fact]
    public void NavigateContentCommand_WithoutContentGroups_KeepsFileSelection()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.NavigateContentCommand.Execute(1);

        viewModel.SelectedIndex.Should().Be(0);
        viewModel.SelectedItem?.Id.Should().Be(FirstItemId);
    }

    [Fact]
    public void NavigateContentCommand_WithZeroDirection_ThrowsArgumentOutOfRangeException()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        Action act = () => viewModel.NavigateContentCommand.Execute(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ContentNavigationPanel_WithSingleStillImage_IsHidden()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    1)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            1,
            ImageFramePresentationModes.None,
            0);

        viewModel.IsContentNavigationPanelVisible.Should().BeFalse();
        viewModel.IsNavigationInformationVisible.Should().BeFalse();
        viewModel.IsImagesOnlyNavigationVisible.Should().BeFalse();
        viewModel.IsAnimationNavigationVisible.Should().BeFalse();
        viewModel.IsContentSelectionVisible.Should().BeFalse();
        viewModel.IsContentNavigationEnabled.Should().BeFalse();
        viewModel.IsFrameSelectionVisible.Should().BeFalse();
        viewModel.SelectedContentText.Should().BeEmpty();
        viewModel.AnimationTimeText.Should().BeEmpty();
    }

    [Fact]
    public void ContentNavigationPanel_WithMultipleStillImages_ShowsImagesOnlyNavigation()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.ManualNavigation,
            0);

        viewModel.IsContentNavigationPanelVisible.Should().BeTrue();
        viewModel.IsNavigationInformationVisible.Should().BeTrue();
        viewModel.IsImagesOnlyNavigationVisible.Should().BeTrue();
        viewModel.IsAnimationNavigationVisible.Should().BeFalse();
        viewModel.IsContentNavigationEnabled.Should().BeTrue();
        viewModel.IsFrameSelectionVisible.Should().BeFalse();
        viewModel.SelectedContentText.Should().Be("Изображение 1/3");
    }

    [Fact]
    public void ContentNavigationPanel_WithSingleAnimation_ShowsOnlyFrames()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    12)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            12,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(12));

        session.AdvanceAnimationFrame();

        viewModel.IsContentNavigationPanelVisible.Should().BeTrue();
        viewModel.IsNavigationInformationVisible.Should().BeTrue();
        viewModel.IsImagesOnlyNavigationVisible.Should().BeFalse();
        viewModel.IsAnimationNavigationVisible.Should().BeTrue();
        viewModel.IsContentSelectionVisible.Should().BeFalse();
        viewModel.IsContentNavigationEnabled.Should().BeFalse();
        viewModel.IsFrameSelectionVisible.Should().BeTrue();
        viewModel.IsAnimationTimeSeparatorVisible.Should().BeFalse();
        viewModel.AnimationTimeText.Should().Be("0:00.1 / 0:01.2");
    }

    [Fact]
    public void ContentNavigationPanel_WithMixedContent_ShowsTypeRelativeNumbers()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(CreateMixedContentGroups(), 0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.ManualNavigation,
            2,
            ImageFrameNumbering.Reverse);

        viewModel.NavigateContentCommand.Execute(1);

        viewModel.SelectedContentText.Should().Be(
            "Изображение 2/3 · Анимаций: 2");
        viewModel.IsImagesOnlyNavigationVisible.Should().BeFalse();
        viewModel.IsAnimationNavigationVisible.Should().BeTrue();
        viewModel.IsContentNavigationEnabled.Should().BeTrue();
        viewModel.IsFrameSelectionVisible.Should().BeFalse();
        viewModel.IsAnimationTimeSeparatorVisible.Should().BeFalse();
    }

    [Fact]
    public void ContentNavigationPanel_WithSelectedAnimation_ShowsAnimationAndFrameNumbers()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(CreateMixedContentGroups(), 2);
        session.SetFramePresentation(
            24,
            ImageFramePresentationModes.AutomaticPlayback,
            3,
            animationTimeline: CreateAnimationTimeline(24));

        viewModel.SelectedContentText.Should().Be(
            "Изображений: 3 · Анимация 2/2");
        viewModel.AnimationTimeText.Should().Be("0:00.3 / 0:02.4");
        viewModel.IsAnimationNavigationVisible.Should().BeTrue();
        viewModel.IsContentNavigationEnabled.Should().BeTrue();
        viewModel.IsFrameSelectionVisible.Should().BeTrue();
        viewModel.IsAnimationTimeSeparatorVisible.Should().BeTrue();
    }

    [Fact]
    public void ContentNavigationPanel_WithMultipleImageGroups_AggregatesImageNumbers()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    2),
                new(
                    ImageContentGroupKind.Animation,
                    8),
                new(
                    ImageContentGroupKind.StillImages,
                    3,
                    ImageFrameNumbering.Reverse)
            }.AsReadOnly(),
            2);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.ManualNavigation,
            2,
            ImageFrameNumbering.Reverse);

        viewModel.SelectedContentText.Should().Be(
            "Изображение 3/5 · Анимаций: 1");
    }

    [Fact]
    public void ContentNavigationPanel_WhileAnimationLoads_HidesStaleFrameInformation()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(CreateMixedContentGroups(), 2);
        session.SetFramePresentation(
            24,
            ImageFramePresentationModes.AutomaticPlayback,
            3);

        session.SetContentGroupLoading(true);

        viewModel.IsContentNavigationPanelVisible.Should().BeTrue();
        viewModel.SelectedContentText.Should().Be(
            "Изображений: 3 · Анимация 2/2");
        viewModel.IsAnimationNavigationVisible.Should().BeTrue();
        viewModel.IsContentNavigationEnabled.Should().BeTrue();
        viewModel.IsFrameSelectionVisible.Should().BeFalse();
        viewModel.IsAnimationTimeSeparatorVisible.Should().BeFalse();
        viewModel.AnimationTimeText.Should().BeEmpty();
    }

    [Fact]
    public void AdvanceAnimationFrame_WithAutomaticPlayback_AdvancesSelection()
    {
        ImageViewerSession session = CreateSession();
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        session.AdvanceAnimationFrame();

        session.SelectedFrameIndex.Should().Be(1);
    }

    [Fact]
    public void AdvanceAnimationFrame_WithReverseNumbering_KeepsDecoderFrameOrder()
    {
        ImageViewerSession session = CreateSession();
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            ImageFrameNumbering.Reverse);

        session.AdvanceAnimationFrame();

        session.SelectedFrameIndex.Should().Be(1);
    }

    [Fact]
    public void SetAnimationBuffering_WhenStateChanges_ExposesUpdatedState()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);

        session.SetAnimationBuffering(true);

        viewModel.IsAnimationBuffering.Should().BeTrue();
    }

    [Fact]
    public void ToggleAnimationPlaybackCommand_WithSelectedAnimation_TogglesPlaybackState()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    4)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);

        viewModel.ToggleAnimationPlaybackCommand.Execute(null);

        viewModel.IsAnimationPlaybackActive.Should().BeFalse();
        viewModel.IsPlayAnimationIconVisible.Should().BeTrue();
        viewModel.IsPauseAnimationIconVisible.Should().BeFalse();
        viewModel.ToggleAnimationPlaybackCommand.CanExecute(null)
            .Should().BeTrue();
    }

    [Fact]
    public void ToggleAnimationPlaybackCommand_WithSelectedStillImage_IsDisabled()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.StillImages,
                    2),
                new(
                    ImageContentGroupKind.Animation,
                    4)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.ManualNavigation,
            0);

        bool canExecute = viewModel
            .ToggleAnimationPlaybackCommand
            .CanExecute(null);

        canExecute.Should().BeFalse();
        viewModel.CanControlAnimationPlayback.Should().BeFalse();
        viewModel.IsPlayAnimationIconVisible.Should().BeTrue();
        viewModel.IsPauseAnimationIconVisible.Should().BeFalse();
    }

    [Fact]
    public void SeekAnimationCommand_WithFractionalPosition_SelectsNearestFrame()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    5)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            5,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(5));

        viewModel.SeekAnimationCommand.Execute(2.6d);

        viewModel.SelectedFrameIndex.Should().Be(3);
        viewModel.AnimationTimelineMaximum.Should().Be(4d);
        viewModel.AnimationTimelineValue.Should().Be(3d);
    }

    [Fact]
    public void SeekAnimationCommand_WithinSelectedFrame_DoesNotChangePlaybackPosition()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(3));
        session.SeekAnimationFrame(1);
        session.SetAnimationPlaybackPosition(
            TimeSpan.FromMilliseconds(150d));

        viewModel.SeekAnimationCommand.Execute(1.4d);

        viewModel.SelectedFrameIndex.Should().Be(1);
        viewModel.AnimationTimelineValue.Should().Be(1d);
        viewModel.AnimationTimeText.Should().Be(
            "0:00.15 / 0:00.30");
    }

    [Fact]
    public void SeekAnimationCommand_ToTimelineEnd_SelectsLastFrame()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(3));

        viewModel.SeekAnimationCommand.Execute(2d);

        viewModel.SelectedFrameIndex.Should().Be(2);
        viewModel.AnimationTimelineMaximum.Should().Be(2d);
        viewModel.AnimationTimelineValue.Should().Be(2d);
        viewModel.AnimationTimeText.Should().Be(
            "0:00.20 / 0:00.30");
    }

    [Fact]
    public void AnimationTimeline_WithThreeFrames_UsesElapsedFrameStartPositions()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    3)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(3));

        double firstFramePosition =
            viewModel.AnimationTimelineValue;
        session.AdvanceAnimationFrame();
        double secondFramePosition =
            viewModel.AnimationTimelineValue;
        session.AdvanceAnimationFrame();
        double thirdFramePosition =
            viewModel.AnimationTimelineValue;

        viewModel.AnimationTimelineMaximum.Should().Be(2d);
        firstFramePosition.Should().Be(0d);
        secondFramePosition.Should().Be(1d);
        thirdFramePosition.Should().Be(2d);
    }

    [Fact]
    public void SelectedFrameChanged_WithAnimation_NotifiesTimelineBindings()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        session.SetContentGroups(
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    5)
            }.AsReadOnly(),
            0);
        session.SetFramePresentation(
            5,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(5));
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, e) =>
            changedProperties.Add(e.PropertyName);

        session.AdvanceAnimationFrame();

        changedProperties.Should().Contain(
            nameof(ImageViewerSessionViewModel.AnimationTimelineValue));
        changedProperties.Should().Contain(
            nameof(ImageViewerSessionViewModel.AnimationTimeText));
        changedProperties.Should().NotContain(
            nameof(ImageViewerSessionViewModel.AnimationTimelineMaximum));
        changedProperties.Should().NotContain(
            nameof(ImageViewerSessionViewModel.AnimationTimeWidthReferenceText));
        viewModel.AnimationTimelineValue.Should().Be(1d);
        viewModel.AnimationTimeText.Should().Be(
            "0:00.10 / 0:00.50");
    }

    [Fact]
    public void ChannelAvailability_WhenAlphaBecomesUnavailable_SelectsBlueChannel()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        viewModel.SelectChannelImageModeCommand.Execute(null);
        session.SetHasAlpha(true);
        viewModel.NavigateCommand.Execute(-1);

        session.SetHasAlpha(false);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Blue);
        viewModel.IsChannelAvailabilityKnown.Should().BeTrue();
    }

    [Fact]
    public void SelectMainImageModeCommand_WhenChannelMode_ClearsSelectedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.SelectMainImageModeCommand.Execute(null);

        viewModel.IsMainImageModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithUnknownSelectedItem_SelectsFirstItem()
    {
        Guid unknownItemId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");
        PicaViewerRequest request = CreateRequest(unknownItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.SelectedIndex.Should().Be(0);
        viewModel.SelectedItem?.Id.Should().Be(FirstItemId);
    }

    [Fact]
    public void Constructor_WithoutItems_HasNoSelectedItem()
    {
        PicaViewerRequest request = new(
            Array.Empty<PicaImageItem>(),
            FirstItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.SelectedIndex.Should().Be(-1);
        viewModel.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void NavigateCommand_WithoutItems_KeepsNoSelection()
    {
        PicaViewerRequest request = new(
            Array.Empty<PicaImageItem>(),
            FirstItemId);
        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.NavigateCommand.Execute(1);

        viewModel.SelectedIndex.Should().Be(-1);
        viewModel.SelectedItem.Should().BeNull();
    }

    private static ImageViewerSessionViewModel CreateViewModel(
        bool isFilteringEnabled = false)
    {
        ImageViewerSession session = new(
            CreateRequest(FirstItemId),
            isFilteringEnabled);

        return new ImageViewerSessionViewModel(session);
    }

    private static ImageViewerSession CreateSession()
    {
        return new ImageViewerSession(
            CreateRequest(FirstItemId),
            false);
    }

    private static IReadOnlyList<ImageContentGroupDefinition>
        CreateMixedContentGroups()
    {
        return new List<ImageContentGroupDefinition>
        {
            new(
                ImageContentGroupKind.StillImages,
                3,
                ImageFrameNumbering.Reverse),
            new(
                ImageContentGroupKind.Animation,
                12),
            new(
                ImageContentGroupKind.Animation,
                24)
        }.AsReadOnly();
    }

    private static ImageAnimationTimeline CreateAnimationTimeline(
        int frameCount)
    {
        return new ImageAnimationTimeline(
            Enumerable
                .Repeat(
                    TimeSpan.FromMilliseconds(100d),
                    frameCount)
                .ToList()
                .AsReadOnly());
    }

    private static PicaViewerRequest CreateRequest(Guid selectedItemId)
    {
        PicaImageItem[] items =
        [
            new PicaImageItem(
                FirstItemId,
                "first.png",
                "first.png"),
            new PicaImageItem(
                SecondItemId,
                "second.png",
                "second.png")
        ];

        return new PicaViewerRequest(
            items,
            selectedItemId);
    }
}

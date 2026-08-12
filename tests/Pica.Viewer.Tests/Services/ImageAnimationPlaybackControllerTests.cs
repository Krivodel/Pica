using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageAnimationPlaybackControllerTests
{
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan FrameDuration =
        TimeSpan.FromMilliseconds(75d);
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);

    [Fact]
    public async Task FramesChanged_WithAutomaticPlayback_AdvancesFrameAfterDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(2));
        using CancellationTokenSource timeout = new(TestTimeout);

        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await WaitForFrameAsync(session, 1, timeout.Token);

        session.SelectedFrameIndex.Should().Be(1);
        delayScheduler.RequestedDurations[0].Should().Be(FrameDuration);
    }

    [Fact]
    public async Task TimelineProgress_WhileFrameIsDisplayed_AdvancesFromFrameStartToFrameEnd()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        ControlledUiFrameScheduler frameScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            frameScheduler,
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 2);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(2));
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        TimeSpan firstFrameTime = TimeSpan.FromSeconds(10d);
        TimeSpan halfFrameDuration = TimeSpan.FromTicks(
            FrameDuration.Ticks / 2L);

        frameScheduler.RunNext(firstFrameTime);
        frameScheduler.RunNext(
            firstFrameTime + halfFrameDuration);

        session.AnimationPosition.Should().Be(
            halfFrameDuration);
        frameScheduler.PendingFrameCount.Should().Be(1);
    }

    [Fact]
    public async Task ToggleAnimationPlayback_AfterPartialFrame_ResumesRemainingDuration()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        ControlledUiFrameScheduler frameScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            frameScheduler,
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 2);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(2));
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        TimeSpan firstFrameTime = TimeSpan.FromSeconds(10d);
        TimeSpan halfFrameDuration = TimeSpan.FromTicks(
            FrameDuration.Ticks / 2L);
        frameScheduler.RunNext(firstFrameTime);
        frameScheduler.RunNext(
            firstFrameTime + halfFrameDuration);

        session.ToggleAnimationPlayback();
        session.ToggleAnimationPlayback();
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        delayScheduler.RequestedDurations.Should().Equal(
            FrameDuration,
            FrameDuration - halfFrameDuration);
        session.AnimationPosition.Should().Be(
            halfFrameDuration);
    }

    [Fact]
    public async Task NavigateFrame_WithAvailableAnimationFrame_RestartsFullFrameDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 3);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            3,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.NavigateFrame(1);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.SelectedFrameIndex.Should().Be(1);
        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        delayScheduler.RequestedDurations.Should().Equal(
            FrameDuration,
            FrameDuration);
    }

    [Fact]
    public async Task NavigateFrame_WithUnavailableAnimationFrame_EntersRemainingBufferingWithoutZeroDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 4);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            4,
            2,
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.NavigateFrame(-1);

        session.SelectedFrameIndex.Should().Be(3);
        session.IsAnimationBuffering.Should().BeTrue();
        controller.State.Should().Be(
            ImageAnimationPlaybackState.RemainingBuffering);
        delayScheduler.RequestedDurations.Should().ContainSingle()
            .Which.Should().Be(FrameDuration);
    }

    [Fact]
    public async Task NavigateFrame_WithUnavailableFrameDuringInitialBuffering_EntersRemainingBufferingImmediately()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 4);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            4,
            1,
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.NavigateFrame(-1);

        session.SelectedFrameIndex.Should().Be(3);
        session.IsAnimationBuffering.Should().BeTrue();
        controller.State.Should().Be(
            ImageAnimationPlaybackState.RemainingBuffering);
        delayScheduler.RequestedDurations.Should().ContainSingle()
            .Which.Should().Be(
                ImageAnimationPlaybackController
                    .InitialBufferingIndicatorDelay);
    }

    [Fact]
    public async Task NavigateFrame_AcrossAnimationBoundary_DoesNotCompleteIteration()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 2);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback,
            animationIterations: 1);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.NavigateFrame(-1);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.SelectedFrameIndex.Should().Be(1);
        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        delayScheduler.RequestedDurations.Should().HaveCount(2);
    }

    [Fact]
    public async Task NavigateFrame_AfterCompletedPlayback_ChangesFrameWithoutRestarting()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 2);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback,
            animationIterations: 1);

        await CompleteScheduledAdvanceAsync(
            delayScheduler,
            uiDispatcher,
            timeout.Token);
        await CompleteScheduledAdvanceAsync(
            delayScheduler,
            uiDispatcher,
            timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Completed);
        session.SelectedFrameIndex.Should().Be(1);

        session.NavigateFrame(-1);
        await Task.Yield();

        session.SelectedFrameIndex.Should().Be(0);
        controller.State.Should().Be(
            ImageAnimationPlaybackState.Completed);
        delayScheduler.RequestedDurations.Should().HaveCount(2);
    }

    [Fact]
    public async Task ToggleAnimationPlayback_WhilePlaying_PausesAndResumesWithFullDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 3);
        session.SetFramePresentation(
            3,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            3,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.ToggleAnimationPlayback();
        session.ToggleAnimationPlayback();
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        session.SelectedFrameIndex.Should().Be(0);
        session.IsAnimationPlaybackActive.Should().BeTrue();
        delayScheduler.RequestedDurations.Should().Equal(
            FrameDuration,
            FrameDuration);
    }

    [Fact]
    public async Task SeekAnimationFrame_WhilePaused_ChangesFrameWithoutResuming()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 4);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            4,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        session.ToggleAnimationPlayback();

        session.SeekAnimationFrame(3);
        await Task.Yield();

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Paused);
        session.SelectedFrameIndex.Should().Be(3);
        session.IsAnimationPlaybackActive.Should().BeFalse();
        delayScheduler.RequestedDurations.Should().ContainSingle();
    }

    [Fact]
    public async Task SeekAnimationFrame_WhilePlaying_RestartsFullFrameDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 4);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            4,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        session.SeekAnimationFrame(3);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        session.SelectedFrameIndex.Should().Be(3);
        session.IsAnimationPlaybackActive.Should().BeTrue();
        delayScheduler.RequestedDurations.Should().Equal(
            FrameDuration,
            FrameDuration);
    }

    [Fact]
    public async Task SeekAnimationFrame_ToUnavailableFrameWhilePaused_WaitsWithoutResuming()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 4);
        session.SetFramePresentation(
            4,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            4,
            2,
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        session.ToggleAnimationPlayback();

        session.SeekAnimationFrame(3);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Seeking);
        session.IsAnimationPlaybackActive.Should().BeFalse();
        session.IsAnimationBuffering.Should().BeTrue();

        frameSource.SetFrameAvailability(3, true);
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Paused);
        session.SelectedFrameIndex.Should().Be(3);
        session.IsAnimationBuffering.Should().BeFalse();
        delayScheduler.RequestedDurations.Should().ContainSingle();
    }

    [Fact]
    public async Task ToggleAnimationPlayback_AfterCompletedPlayback_RestartsFromFirstFrame()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        SetAnimationContent(session, 2);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback,
            animationIterations: 1);
        await CompleteScheduledAdvanceAsync(
            delayScheduler,
            uiDispatcher,
            timeout.Token);
        await CompleteScheduledAdvanceAsync(
            delayScheduler,
            uiDispatcher,
            timeout.Token);

        session.ToggleAnimationPlayback();
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        session.SelectedFrameIndex.Should().Be(0);
        session.IsAnimationPlaybackActive.Should().BeTrue();
        delayScheduler.RequestedDurations.Should().HaveCount(3);
    }

    [Fact]
    public async Task FramesChanged_WithoutAutomaticPlayback_DoesNotScheduleDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.ManualNavigation,
            0);

        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.ManualNavigation);
        await Task.Yield();

        delayScheduler.RequestedDurations.Should().BeEmpty();
        session.SelectedFrameIndex.Should().Be(0);
    }

    [Fact]
    public async Task FramesChanged_WithSingleIteration_StopsOnLastFrame()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            2,
            ImageFramePresentationModes.AutomaticPlayback,
            0,
            animationTimeline: CreateAnimationTimeline(2));
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetFrames(
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback,
            animationIterations: 1);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        session.SelectedFrameIndex.Should().Be(1);
        session.AnimationPosition.Should().Be(
            FrameDuration * 2d);
        delayScheduler.RequestedDurations.Should().HaveCount(2);
    }

    [Fact]
    public async Task FramesChanged_WhileInitialBufferIsIncomplete_DelaysBufferingIndicator()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            10,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);

        frameSource.SetProgressiveFrames(
            10,
            2,
            8,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.InitialBuffering);
        session.IsAnimationBuffering.Should().BeFalse();
        delayScheduler.RequestedDurations.Should().ContainSingle()
            .Which.Should().Be(
                ImageAnimationPlaybackController
                    .InitialBufferingIndicatorDelay);

        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        session.IsAnimationBuffering.Should().BeTrue();
    }

    [Fact]
    public async Task FrameAvailabilityChanged_WhenInitialBufferBecomesReadyBeforeIndicatorDelay_StartsPlaybackWithoutIndicator()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            new InlineViewerUiDispatcher(),
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            10,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            10,
            2,
            8,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);

        for (int frameIndex = 2;
            frameIndex < 8;
            frameIndex++)
        {
            frameSource.SetFrameAvailability(
                frameIndex,
                true);
        }

        await delayScheduler.WaitForRequestAsync(timeout.Token);

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        session.IsAnimationBuffering.Should().BeFalse();
        delayScheduler.RequestedDurations.Should().Equal(
            ImageAnimationPlaybackController
                .InitialBufferingIndicatorDelay,
            FrameDuration);
    }

    [Fact]
    public async Task FrameAvailabilityChanged_AfterPlaybackExhaustsBuffer_WaitsForRefilledBufferAndFullDelay()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            8,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            8,
            3,
            2,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        session.SelectedFrameIndex.Should().Be(2);
        session.IsAnimationBuffering.Should().BeTrue();
        controller.State.Should().Be(
            ImageAnimationPlaybackState.RemainingBuffering);
        delayScheduler.RequestedDurations.Should().HaveCount(3);

        frameSource.SetFrameAvailability(3, true);
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        session.SelectedFrameIndex.Should().Be(2);
        controller.State.Should().Be(
            ImageAnimationPlaybackState.Playing);
        session.IsAnimationBuffering.Should().BeFalse();
        delayScheduler.RequestedDurations.Should().HaveCount(4);
        delayScheduler.RequestedDurations[3].Should().Be(
            FrameDuration);

        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        session.SelectedFrameIndex.Should().Be(3);
    }

    [Fact]
    public async Task FrameAvailabilityChanged_WhenInitialDecodingFails_HidesIndicatorAndStopsPlayback()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        using ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            10,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            10,
            2,
            8,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        frameSource.CompleteDecodingWithMissingFrames();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        controller.State.Should().Be(
            ImageAnimationPlaybackState.Failed);
        session.IsAnimationBuffering.Should().BeFalse();
        session.SelectedFrameIndex.Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_WhileBufferingIndicatorIsVisible_HidesIndicator()
    {
        ImageViewerSession session = CreateSession();
        StubImageFrameSource frameSource = new();
        ControlledImageAnimationDelayScheduler delayScheduler = new();
        using ControlledViewerUiDispatcher uiDispatcher = new();
        ImageAnimationPlaybackController controller = new(
            session,
            frameSource,
            delayScheduler,
            uiDispatcher,
            new ControlledUiFrameScheduler(),
            NullLogger<ImageAnimationPlaybackController>.Instance);
        session.SetFramePresentation(
            10,
            ImageFramePresentationModes.AutomaticPlayback,
            0);
        using CancellationTokenSource timeout = new(TestTimeout);
        frameSource.SetProgressiveFrames(
            10,
            2,
            8,
            FrameDuration,
            ImageFramePresentationModes.AutomaticPlayback);
        await delayScheduler.WaitForRequestAsync(timeout.Token);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(timeout.Token);
        uiDispatcher.RunNext();

        await controller.DisposeAsync(timeout.Token);

        session.IsAnimationBuffering.Should().BeFalse();
        controller.State.Should().Be(
            ImageAnimationPlaybackState.Idle);
    }

    private static ImageViewerSession CreateSession()
    {
        PicaImageItem item = new(
            ItemId,
            "image.gif",
            "image.gif");
        PicaViewerRequest request = new(
            new List<PicaImageItem> { item },
            item.Id);

        return new ImageViewerSession(request, true);
    }

    private static void SetAnimationContent(
        ImageViewerSession session,
        int frameCount)
    {
        IReadOnlyList<ImageContentGroupDefinition> groups =
            new List<ImageContentGroupDefinition>
            {
                new(
                    ImageContentGroupKind.Animation,
                    frameCount)
            }.AsReadOnly();

        session.SetContentGroups(groups, 0);
    }

    private static ImageAnimationTimeline CreateAnimationTimeline(
        int frameCount)
    {
        return new ImageAnimationTimeline(
            Enumerable
                .Repeat(FrameDuration, frameCount)
                .ToList()
                .AsReadOnly());
    }

    private static async Task CompleteScheduledAdvanceAsync(
        ControlledImageAnimationDelayScheduler delayScheduler,
        ControlledViewerUiDispatcher uiDispatcher,
        CancellationToken ct)
    {
        await delayScheduler.WaitForRequestAsync(ct);
        delayScheduler.CompleteNext();
        await uiDispatcher.WaitForPendingAsync(ct);
        uiDispatcher.RunNext();
    }

    private static async Task SetFrameAvailableAsync(
        StubImageFrameSource frameSource,
        int frameIndex,
        ControlledViewerUiDispatcher uiDispatcher,
        CancellationToken ct)
    {
        frameSource.SetFrameAvailability(
            frameIndex,
            true);
        await uiDispatcher.WaitForPendingAsync(ct);
        uiDispatcher.RunNext();
    }

    private static async Task WaitForFrameAsync(
        ImageViewerSession session,
        int expectedFrameIndex,
        CancellationToken ct)
    {
        while (session.SelectedFrameIndex != expectedFrameIndex)
        {
            await Task.Delay(1, ct);
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImagePresentationControllerTests
{
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task ChannelModeWorkflow_WithFullResolutionBitmap_LoadsLazilyAndRestoresSource()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                RecordingImageChannelBitmapLoader loader = new();
                using ImagePresentationController controller = new(
                    session,
                    loader,
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap sourceBitmap = BgraBitmapTestData.CreateBitmap();
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");

                controller.ReplaceFullResolutionBitmap(item, sourceBitmap);

                loader.AlphaReadCount.Should().Be(0);
                loader.ChannelLoadCount.Should().Be(0);
                controller.DisplayedBitmap.Should().BeSameAs(sourceBitmap);

                viewModel.SelectChannelImageModeCommand.Execute(null);
                await controller.WaitForSelectedChannelAsync(
                    CancellationToken.None);

                loader.AlphaReadCount.Should().Be(1);
                loader.ChannelLoadCount.Should().Be(1);
                loader.LastChannel.Should().Be(ImageChannel.Red);
                controller.DisplayedBitmap.Should().NotBeSameAs(sourceBitmap);
                controller.DisplayedChannel.Should().Be(ImageChannel.Red);

                viewModel.SelectMainImageModeCommand.Execute(null);

                controller.DisplayedBitmap.Should().BeSameAs(sourceBitmap);
                controller.DisplayedChannel.Should().BeNull();
            });
    }

    [Fact]
    public async Task ChannelModeWorkflow_WithAsynchronousLoader_UpdatesPresentationOnUiThread()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                RecordingImageChannelBitmapLoader loader = new()
                {
                    CompleteAsynchronously = true
                };
                using ImagePresentationController controller = new(
                    session,
                    loader,
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap sourceBitmap = BgraBitmapTestData.CreateBitmap();
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                bool? channelUpdateHasUiThreadAccess = null;
                controller.Changed += (_, _) =>
                {
                    if (controller.DisplayedChannel is not null)
                    {
                        channelUpdateHasUiThreadAccess =
                            Dispatcher.UIThread.CheckAccess();
                    }
                };

                controller.ReplaceFullResolutionBitmap(item, sourceBitmap);
                viewModel.SelectChannelImageModeCommand.Execute(null);
                await controller.WaitForSelectedChannelAsync(
                    CancellationToken.None);

                channelUpdateHasUiThreadAccess.Should().BeTrue();
            });
    }

    [Fact]
    public async Task AcquireDisplayedBitmap_WhenModeChanges_KeepsBitmapAliveUntilReleased()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                RecordingImageChannelBitmapLoader loader = new();
                using ImagePresentationController controller = new(
                    session,
                    loader,
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap sourceBitmap = BgraBitmapTestData.CreateBitmap();
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionBitmap(item, sourceBitmap);
                viewModel.SelectChannelImageModeCommand.Execute(null);
                await controller.WaitForSelectedChannelAsync(
                    CancellationToken.None);
                using ImagePresentationBitmapLease bitmapLease =
                    controller.AcquireDisplayedBitmap(ImageChannel.Red)
                    ?? throw new InvalidOperationException(
                        "The selected channel bitmap must be available.");

                viewModel.SelectMainImageModeCommand.Execute(null);
                PreparedClipboardImage preparedImage =
                    await new ClipboardImagePreparer().PrepareImageAsync(
                        bitmapLease.Bitmap,
                        CancellationToken.None);

                preparedImage.PngContent.Should().NotBeEmpty();
            });
    }

    [Fact]
    public async Task DisposeAsync_WhenChannelLoadIsBlocked_WaitsAndDisposesResult()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                using PicaTemporaryDirectory temporaryDirectory = new();
                string bitmapPath = await CreateBitmapFileAsync(
                    temporaryDirectory.DirectoryPath);
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                ControlledImageChannelBitmapLoader loader = new();
                using ImagePresentationController controller = new(
                    session,
                    loader,
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap sourceBitmap = BgraBitmapTestData.CreateBitmap();
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                using CancellationTokenSource timeout = new(TestTimeout);
                controller.ReplaceFullResolutionBitmap(item, sourceBitmap);
                viewModel.SelectChannelImageModeCommand.Execute(null);
                await loader.WaitUntilStartedAsync(timeout.Token);

                Task disposalTask = controller.DisposeAsync(timeout.Token);

                loader.IsCancellationRequested.Should().BeTrue();
                disposalTask.IsCompleted.Should().BeFalse();
                TrackingBitmap channelBitmap = new(bitmapPath);
                loader.Complete(channelBitmap);
                await disposalTask;

                channelBitmap.IsDisposed.Should().BeTrue();
                controller.SourceBitmap.Should().BeNull();
                controller.DisplayedBitmap.Should().BeNull();
            });
    }

    [Fact]
    public async Task DisposeAsync_WithActiveBitmapLease_WaitsForLeaseRelease()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                using PicaTemporaryDirectory temporaryDirectory = new();
                string bitmapPath = await CreateBitmapFileAsync(
                    temporaryDirectory.DirectoryPath);
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                TrackingBitmap sourceBitmap = new(bitmapPath);
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionBitmap(item, sourceBitmap);
                using ImagePresentationBitmapLease bitmapLease =
                    controller.AcquireDisplayedBitmap(null)
                    ?? throw new InvalidOperationException(
                        "The source bitmap lease must be available.");
                using CancellationTokenSource timeout = new(TestTimeout);

                Task disposalTask = controller.DisposeAsync(timeout.Token);

                disposalTask.IsCompleted.Should().BeFalse();
                sourceBitmap.IsDisposed.Should().BeFalse();
                bitmapLease.Dispose();
                await disposalTask;

                sourceBitmap.IsDisposed.Should().BeTrue();
            });
    }

    [Fact]
    public async Task NavigateFrame_WithAnimationFrames_DisplaysSelectedFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstBitmap = BgraBitmapTestData.CreateBitmap();
                Bitmap secondBitmap = BgraBitmapTestData.CreateBitmap();
                List<DecodedImageFrame> frames =
                [
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.FromMilliseconds(100d)),
                    new DecodedImageFrame(
                        secondBitmap,
                        TimeSpan.FromMilliseconds(100d))
                ];
                DecodedImage image = new(
                    frames,
                    ImageFramePresentationModes.AutomaticPlayback,
                    0);
                PicaImageItem item = viewModel.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionImage(item, image);

                viewModel.NavigateFrameCommand.Execute(1);

                controller.DisplayedBitmap.Should().BeSameAs(secondBitmap);
                controller.SourceBitmap.Should().BeSameAs(secondBitmap);
                viewModel.SelectedFrameIndex.Should().Be(1);
                viewModel.FrameCount.Should().Be(2);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task ReplaceFullResolutionImage_WithPreferredFrame_DisplaysPreferredFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            () =>
            {
                ImageViewerSession session = CreateSession();
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap preferredBitmap =
                    BgraBitmapTestData.CreateBitmap();
                List<DecodedImageFrame> frames =
                [
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.Zero),
                    new DecodedImageFrame(
                        preferredBitmap,
                        TimeSpan.Zero)
                ];
                DecodedImage image = new(
                    frames,
                    ImageFramePresentationModes.ManualNavigation,
                    0,
                    1,
                    ImageFrameNumbering.Reverse);
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");

                controller.ReplaceFullResolutionImage(
                    item,
                    image);

                controller.DisplayedBitmap.Should().BeSameAs(
                    preferredBitmap);
                session.SelectedFrameIndex.Should().Be(1);
                session.FrameNumbering.Should().Be(
                    ImageFrameNumbering.Reverse);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task AddFrame_WhenSelectedProgressiveFrameBecomesAvailable_DisplaysFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ControlledViewerUiDispatcher uiDispatcher = new();
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    uiDispatcher,
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap thirdBitmap =
                    BgraBitmapTestData.CreateBitmap();
                DecodedImage image =
                    DecodedImage.CreateProgressive(
                        3,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2);
                image.AddFrame(
                    0,
                    new DecodedImageFrame(
                        firstBitmap,
                        TimeSpan.Zero));
                image.AddFrame(
                    1,
                    new DecodedImageFrame(
                        secondBitmap,
                        TimeSpan.Zero));
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionImage(item, image);
                viewModel.NavigateFrameCommand.Execute(1);
                viewModel.NavigateFrameCommand.Execute(1);
                using CancellationTokenSource timeout =
                    new(TestTimeout);

                controller.DisplayedBitmap.Should().BeSameAs(
                    secondBitmap);

                image.AddFrame(
                    2,
                    new DecodedImageFrame(
                        thirdBitmap,
                        TimeSpan.Zero));
                await uiDispatcher.WaitForPendingAsync(
                    timeout.Token);
                uiDispatcher.RunNext();

                controller.DisplayedBitmap.Should().BeSameAs(
                    thirdBitmap);
                session.SelectedFrameIndex.Should().Be(2);
            });
    }

    [Fact]
    public async Task NavigateContent_WithMixedContent_CyclesImagesAndAnimations()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstStillBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondStillBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap firstAnimationBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondAnimationBitmap =
                    BgraBitmapTestData.CreateBitmap();
                DecodedImage stillImages = new(
                    [
                        new DecodedImageFrame(
                            firstStillBitmap,
                            TimeSpan.Zero),
                        new DecodedImageFrame(
                            secondStillBitmap,
                            TimeSpan.Zero)
                    ],
                    ImageFramePresentationModes.ManualNavigation,
                    0);
                DecodedImage firstAnimation = new(
                    [
                        new DecodedImageFrame(
                            firstAnimationBitmap,
                            TimeSpan.FromMilliseconds(100d))
                    ],
                    ImageFramePresentationModes.AutomaticPlayback,
                    0);
                DecodedImage secondAnimation = new(
                    [
                        new DecodedImageFrame(
                            secondAnimationBitmap,
                            TimeSpan.FromMilliseconds(100d))
                    ],
                    ImageFramePresentationModes.AutomaticPlayback,
                    0);
                DecodedImageContent content = new(
                    [
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.StillImages,
                                2),
                            stillImages),
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.Animation,
                                1),
                            firstAnimation),
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.Animation,
                                1),
                            secondAnimation)
                    ],
                    0);
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionContent(item, content);

                viewModel.NavigateContentCommand.Execute(1);
                controller.DisplayedBitmap.Should().BeSameAs(
                    secondStillBitmap);

                viewModel.NavigateContentCommand.Execute(1);
                controller.DisplayedBitmap.Should().BeSameAs(
                    firstAnimationBitmap);

                viewModel.NavigateContentCommand.Execute(1);
                controller.DisplayedBitmap.Should().BeSameAs(
                    secondAnimationBitmap);

                viewModel.NavigateContentCommand.Execute(1);

                controller.DisplayedBitmap.Should().BeSameAs(
                    firstStillBitmap);
                viewModel.SelectedFrameIndex.Should().Be(0);
                viewModel.CanNavigateFrames.Should().BeFalse();

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task NavigateContent_WhenReturningToAdvancedAnimation_RestartsFromRetainedInitialFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstFrameBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondFrameBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap thirdFrameBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap otherAnimationBitmap =
                    BgraBitmapTestData.CreateBitmap();
                DecodedImage firstAnimation =
                    DecodedImage.CreateProgressive(
                        5,
                        ImageFramePresentationModes.AutomaticPlayback,
                        0,
                        2);
                firstAnimation.AddFrame(
                    0,
                    new DecodedImageFrame(
                        firstFrameBitmap,
                        TimeSpan.FromMilliseconds(100d)));
                firstAnimation.AddFrame(
                    1,
                    new DecodedImageFrame(
                        secondFrameBitmap,
                        TimeSpan.FromMilliseconds(100d)));
                DecodedImage secondAnimation =
                    DecodedImage.CreateSingle(
                        otherAnimationBitmap);
                DecodedImageContent content = new(
                    [
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.Animation,
                                5),
                            firstAnimation),
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.Animation,
                                1),
                            secondAnimation)
                    ],
                    0);
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionContent(item, content);
                viewModel.NavigateFrameCommand.Execute(1);
                firstAnimation.AddFrame(
                    2,
                    new DecodedImageFrame(
                        thirdFrameBitmap,
                        TimeSpan.FromMilliseconds(100d)));
                viewModel.NavigateFrameCommand.Execute(1);

                viewModel.NavigateContentCommand.Execute(1);
                viewModel.NavigateContentCommand.Execute(1);

                controller.DisplayedBitmap.Should().BeSameAs(
                    firstFrameBitmap);
                session.SelectedContentGroupIndex.Should().Be(0);
                session.SelectedFrameIndex.Should().Be(0);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task NavigateContent_WhileStillImagesLoad_AppliesLatestImageSelection()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                ImageViewerSession session = CreateSession();
                using ImageViewerSessionViewModel viewModel = new(session);
                using ControlledViewerUiDispatcher uiDispatcher = new();
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    uiDispatcher,
                    NullLogger<ImagePresentationController>.Instance);
                Bitmap firstStillBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap secondStillBitmap =
                    BgraBitmapTestData.CreateBitmap();
                Bitmap animationBitmap =
                    BgraBitmapTestData.CreateBitmap();
                DecodedImage stillImages = new(
                    [
                        new DecodedImageFrame(
                            firstStillBitmap,
                            TimeSpan.Zero),
                        new DecodedImageFrame(
                            secondStillBitmap,
                            TimeSpan.Zero)
                    ],
                    ImageFramePresentationModes.ManualNavigation,
                    0);
                DecodedImage animation = DecodedImage.CreateSingle(
                    animationBitmap);
                TaskCompletionSource<DecodedImage> stillImagesCompletion =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);
                DecodedImageContent content = new(
                    [
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.StillImages,
                                2),
                            _ => stillImagesCompletion.Task),
                        new DecodedImageContentGroup(
                            new ImageContentGroupDefinition(
                                ImageContentGroupKind.Animation,
                                1),
                            animation)
                    ],
                    1);
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionContent(item, content);
                using CancellationTokenSource timeout = new(TestTimeout);

                viewModel.NavigateContentCommand.Execute(-1);
                viewModel.NavigateContentCommand.Execute(-1);
                stillImagesCompletion.SetResult(stillImages);
                await uiDispatcher.WaitForPendingAsync(timeout.Token);
                uiDispatcher.RunNext();

                controller.DisplayedBitmap.Should().BeSameAs(
                    firstStillBitmap);
                session.SelectedContentGroupIndex.Should().Be(0);
                session.SelectedFrameIndex.Should().Be(0);
            });
    }

    [Fact]
    public async Task DisposeAsync_WithMultipleFrames_DisposesEveryFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                using PicaTemporaryDirectory temporaryDirectory = new();
                string bitmapPath = await CreateBitmapFileAsync(
                    temporaryDirectory.DirectoryPath);
                ImageViewerSession session = CreateSession();
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                TrackingBitmap firstBitmap = new(bitmapPath);
                TrackingBitmap secondBitmap = new(bitmapPath);
                List<DecodedImageFrame> frames =
                [
                    new DecodedImageFrame(firstBitmap, TimeSpan.Zero),
                    new DecodedImageFrame(secondBitmap, TimeSpan.Zero)
                ];
                DecodedImage image = new(
                    frames,
                    ImageFramePresentationModes.ManualNavigation,
                    0);
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionImage(item, image);
                using CancellationTokenSource timeout = new(TestTimeout);

                await controller.DisposeAsync(timeout.Token);

                firstBitmap.IsDisposed.Should().BeTrue();
                secondBitmap.IsDisposed.Should().BeTrue();
            });
    }

    [Fact]
    public async Task ReplaceFullResolutionImage_WithPreviousImage_DisposesEveryPreviousFrame()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePresentationControllerTests),
            SessionLock,
            async () =>
            {
                using PicaTemporaryDirectory temporaryDirectory = new();
                string bitmapPath = await CreateBitmapFileAsync(
                    temporaryDirectory.DirectoryPath);
                ImageViewerSession session = CreateSession();
                using ImagePresentationController controller = new(
                    session,
                    new RecordingImageChannelBitmapLoader(),
                    new AvaloniaViewerUiDispatcher(),
                    NullLogger<ImagePresentationController>.Instance);
                TrackingBitmap firstBitmap = new(bitmapPath);
                TrackingBitmap secondBitmap = new(bitmapPath);
                DecodedImage firstImage = new(
                    [
                        new DecodedImageFrame(
                            firstBitmap,
                            TimeSpan.Zero),
                        new DecodedImageFrame(
                            secondBitmap,
                            TimeSpan.Zero)
                    ],
                    ImageFramePresentationModes.ManualNavigation,
                    0);
                Bitmap replacementBitmap =
                    BgraBitmapTestData.CreateBitmap();
                PicaImageItem item = session.SelectedItem
                    ?? throw new InvalidOperationException(
                        "The test session must contain a selected image.");
                controller.ReplaceFullResolutionImage(
                    item,
                    firstImage);

                controller.ReplaceFullResolutionBitmap(
                    item,
                    replacementBitmap);
                using CancellationTokenSource timeout =
                    new(TestTimeout);

                while (!firstBitmap.IsDisposed
                    || !secondBitmap.IsDisposed)
                {
                    await Task.Delay(1, timeout.Token);
                }

                firstBitmap.IsDisposed.Should().BeTrue();
                secondBitmap.IsDisposed.Should().BeTrue();
            });
    }

    private static ImageViewerSession CreateSession()
    {
        PicaImageItem item = new(
            ItemId,
            "image.png",
            "image.png");
        PicaViewerRequest request = new(
            new PicaImageItem[] { item },
            ItemId);

        return new ImageViewerSession(request, true);
    }

    private static async Task<string> CreateBitmapFileAsync(
        string directoryPath)
    {
        string bitmapPath = Path.Combine(
            directoryPath,
            "channel.png");
        using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
        byte[] content = await new PngImageEncoder().EncodeAsync(
            bitmap,
            CancellationToken.None);
        await File.WriteAllBytesAsync(bitmapPath, content);

        return bitmapPath;
    }
}

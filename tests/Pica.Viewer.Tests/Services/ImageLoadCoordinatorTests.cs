using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImageLoadCoordinatorTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdItemId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task Start_WithStandardLoading_AppliesFullResolutionImage()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                false);

            coordinator.Start();
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                CancellationToken.None);

            isReady.Should().BeTrue();
            presentationSink.BeginCount.Should().Be(1);
            presentationSink.PreviewCount.Should().Be(0);
            presentationSink.FullResolutionCount.Should().Be(1);
            presentationSink.LastItem.Should().Be(item);
            frameAwaiter.WaitCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task Start_WithFastLoading_AppliesPreviewBeforeFullResolution()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                true);

            coordinator.Start();
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                CancellationToken.None);

            isReady.Should().BeTrue();
            presentationSink.PreviewCount.Should().Be(1);
            presentationSink.FullResolutionCount.Should().Be(1);
            frameAwaiter.WaitCount.Should().Be(1);
            frameAwaiter.WaitHasUiThreadAccess.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Start_WithMissingImage_LeavesFullResolutionUnavailable()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "missing.png");
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "missing.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                false);

            coordinator.Start();
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                CancellationToken.None);

            isReady.Should().BeFalse();
            presentationSink.BeginCount.Should().Be(1);
            presentationSink.PreviewCount.Should().Be(0);
            presentationSink.FullResolutionCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task WaitForFullResolutionAsync_WhenCallerCancels_DoesNotCancelImageLoad()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader,
                new InlineViewerUiDispatcher(),
                false);
            using CancellationTokenSource timeout = new(TestTimeout);
            using CancellationTokenSource callerCancellation = new();
            coordinator.Start();
            await fullResolutionLoader.WaitUntilStartedAsync(
                imagePath,
                timeout.Token);
            callerCancellation.Cancel();

            Func<Task> wait = async () =>
                await coordinator.WaitForFullResolutionAsync(
                    callerCancellation.Token);

            await wait.Should().ThrowAsync<OperationCanceledException>();
            fullResolutionLoader
                .GetCancellationToken(imagePath)
                .IsCancellationRequested.Should().BeFalse();
            TrackingBitmap bitmap = new(imagePath);
            fullResolutionLoader.Complete(imagePath, bitmap);
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                timeout.Token);

            isReady.Should().BeTrue();
            bitmap.IsDisposed.Should().BeFalse();
            presentationSink.FullResolutionBitmap.Should().BeSameAs(bitmap);
        });
    }

    [Fact]
    public async Task SelectedIndex_WhenPreviousLoadIsRunning_CancelsAndDisposesObsoleteResult()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string firstPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "first.png");
            string secondPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "second.png");
            PicaImageItem firstItem = new(
                ItemId,
                firstPath,
                "first.png");
            PicaImageItem secondItem = new(
                SecondItemId,
                secondPath,
                "second.png");
            List<PicaImageItem> items = [firstItem, secondItem];
            ImageViewerSession session = CreateSession(items, firstItem.Id);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { firstPath, secondPath });
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader,
                new InlineViewerUiDispatcher(),
                false);
            using CancellationTokenSource timeout = new(TestTimeout);
            coordinator.Start();
            await fullResolutionLoader.WaitUntilStartedAsync(
                firstPath,
                timeout.Token);

            session.Navigate(1);

            await fullResolutionLoader.WaitUntilStartedAsync(
                secondPath,
                timeout.Token);
            fullResolutionLoader
                .GetCancellationToken(firstPath)
                .IsCancellationRequested.Should().BeTrue();
            fullResolutionLoader
                .GetCancellationToken(secondPath)
                .IsCancellationRequested.Should().BeFalse();
            TrackingBitmap obsoleteBitmap = new(firstPath);
            fullResolutionLoader.Complete(firstPath, obsoleteBitmap);
            await obsoleteBitmap.WaitForDisposalAsync(timeout.Token);
            TrackingBitmap selectedBitmap = new(secondPath);
            fullResolutionLoader.Complete(secondPath, selectedBitmap);
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                timeout.Token);

            isReady.Should().BeTrue();
            obsoleteBitmap.IsDisposed.Should().BeTrue();
            selectedBitmap.IsDisposed.Should().BeFalse();
            presentationSink.FullResolutionCount.Should().Be(1);
            presentationSink.LastItem.Should().Be(secondItem);
            presentationSink.FullResolutionBitmap.Should().BeSameAs(
                selectedBitmap);
        });
    }

    [Fact]
    public async Task Dispose_WhenLoadCompletesAfterCancellation_DisposesUnappliedBitmap()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader,
                new InlineViewerUiDispatcher(),
                false);
            using CancellationTokenSource timeout = new(TestTimeout);

            try
            {
                coordinator.Start();
                await fullResolutionLoader.WaitUntilStartedAsync(
                    imagePath,
                    timeout.Token);

                coordinator.Dispose();

                fullResolutionLoader
                    .GetCancellationToken(imagePath)
                    .IsCancellationRequested.Should().BeTrue();
                TrackingBitmap bitmap = new(imagePath);
                fullResolutionLoader.Complete(imagePath, bitmap);
                await bitmap.WaitForDisposalAsync(timeout.Token);

                bitmap.IsDisposed.Should().BeTrue();
                presentationSink.FullResolutionCount.Should().Be(0);
            }
            finally
            {
                coordinator.Dispose();
            }
        });
    }

    [Fact]
    public async Task DisposeAsync_WhenFullResolutionLoadIsBlocked_WaitsForCancellationCleanup()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            ImageViewerSession session = CreateSession(item);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader,
                new InlineViewerUiDispatcher(),
                false);
            using CancellationTokenSource timeout = new(TestTimeout);
            coordinator.Start();
            await fullResolutionLoader.WaitUntilStartedAsync(
                imagePath,
                timeout.Token);

            Task disposalTask = coordinator.DisposeAsync(timeout.Token);

            fullResolutionLoader
                .GetCancellationToken(imagePath)
                .IsCancellationRequested.Should().BeTrue();
            disposalTask.IsCompleted.Should().BeFalse();
            TrackingBitmap bitmap = new(imagePath);
            fullResolutionLoader.Complete(imagePath, bitmap);
            await disposalTask;

            bitmap.IsDisposed.Should().BeTrue();
            presentationSink.FullResolutionCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task DisposeAsync_WhenAdjacentPreloadIsRunning_DisposesCachedPreview()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string selectedPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "selected.png");
            string nextPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "next.png");
            string previousPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "previous.png");
            PicaImageItem selectedItem = new(
                ItemId,
                selectedPath,
                "selected.png");
            PicaImageItem nextItem = new(
                SecondItemId,
                nextPath,
                "next.png");
            PicaImageItem previousItem = new(
                ThirdItemId,
                previousPath,
                "previous.png");
            List<PicaImageItem> items =
                [selectedItem, nextItem, previousItem];
            ImageViewerSession session = CreateSession(
                items,
                selectedItem.Id);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledImagePreviewLoader previewLoader = new(items);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { selectedPath });
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                previewLoader,
                fullResolutionLoader,
                new InlineViewerUiDispatcher(),
                true);
            using CancellationTokenSource timeout = new(TestTimeout);
            coordinator.Start();
            await previewLoader.WaitUntilStartedAsync(
                selectedItem,
                timeout.Token);
            TrackingBitmap selectedPreviewBitmap = new(selectedPath);
            previewLoader.Complete(
                selectedItem,
                new DecodedImagePreview(
                    selectedPreviewBitmap,
                    selectedPreviewBitmap.PixelSize));
            await fullResolutionLoader.WaitUntilStartedAsync(
                selectedPath,
                timeout.Token);
            TrackingBitmap fullResolutionBitmap = new(selectedPath);
            fullResolutionLoader.Complete(
                selectedPath,
                fullResolutionBitmap);
            await previewLoader.WaitUntilStartedAsync(
                nextItem,
                timeout.Token);
            TrackingBitmap cachedPreviewBitmap = new(nextPath);
            previewLoader.Complete(
                nextItem,
                new DecodedImagePreview(
                    cachedPreviewBitmap,
                    cachedPreviewBitmap.PixelSize));
            await previewLoader.WaitUntilStartedAsync(
                previousItem,
                timeout.Token);

            await coordinator.DisposeAsync(timeout.Token);

            cachedPreviewBitmap.IsDisposed.Should().BeTrue();
            presentationSink.FullResolutionBitmap.Should().BeSameAs(
                fullResolutionBitmap);
        });
    }

    [Fact]
    public async Task SelectedIndex_WhenObsoleteApplyIsQueued_DiscardsAndDisposesObsoleteBitmap()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string firstPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "first.png");
            string secondPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "second.png");
            PicaImageItem firstItem = new(
                ItemId,
                firstPath,
                "first.png");
            PicaImageItem secondItem = new(
                SecondItemId,
                secondPath,
                "second.png");
            List<PicaImageItem> items = [firstItem, secondItem];
            ImageViewerSession session = CreateSession(items, firstItem.Id);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { firstPath, secondPath });
            using ControlledViewerUiDispatcher uiDispatcher = new();
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader,
                uiDispatcher,
                false);
            using CancellationTokenSource timeout = new(TestTimeout);
            coordinator.Start();
            await fullResolutionLoader.WaitUntilStartedAsync(
                firstPath,
                timeout.Token);
            TrackingBitmap obsoleteBitmap = new(firstPath);
            fullResolutionLoader.Complete(firstPath, obsoleteBitmap);
            await uiDispatcher.WaitForPendingAsync(timeout.Token);

            session.Navigate(1);

            await fullResolutionLoader.WaitUntilStartedAsync(
                secondPath,
                timeout.Token);
            uiDispatcher.RunNext();
            await obsoleteBitmap.WaitForDisposalAsync(timeout.Token);
            TrackingBitmap selectedBitmap = new(secondPath);
            fullResolutionLoader.Complete(secondPath, selectedBitmap);
            await uiDispatcher.WaitForPendingAsync(timeout.Token);
            uiDispatcher.RunNext();
            bool isReady = await coordinator.WaitForFullResolutionAsync(
                timeout.Token);

            isReady.Should().BeTrue();
            obsoleteBitmap.IsDisposed.Should().BeTrue();
            selectedBitmap.IsDisposed.Should().BeFalse();
            presentationSink.FullResolutionCount.Should().Be(1);
            presentationSink.LastItem.Should().Be(secondItem);
            presentationSink.FullResolutionBitmap.Should().BeSameAs(
                selectedBitmap);
        });
    }

    [Fact]
    public async Task SelectedIndex_WhenObsoletePreviewApplyIsQueued_DiscardsAndDisposesPreview()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string firstPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "first.png");
            string secondPath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath,
                "second.png");
            PicaImageItem firstItem = new(
                ItemId,
                firstPath,
                "first.png");
            PicaImageItem secondItem = new(
                SecondItemId,
                secondPath,
                "second.png");
            List<PicaImageItem> items = [firstItem, secondItem];
            ImageViewerSession session = CreateSession(items, firstItem.Id);
            using RecordingImageLoadPresentationSink presentationSink = new();
            RecordingViewerRenderFrameAwaiter frameAwaiter = new();
            ControlledImagePreviewLoader previewLoader = new(items);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { firstPath, secondPath });
            using ControlledViewerUiDispatcher uiDispatcher = new();
            using ImageLoadCoordinator coordinator = CreateCoordinator(
                session,
                presentationSink,
                frameAwaiter,
                previewLoader,
                fullResolutionLoader,
                uiDispatcher,
                true);
            using CancellationTokenSource timeout = new(TestTimeout);
            coordinator.Start();
            await previewLoader.WaitUntilStartedAsync(
                firstItem,
                timeout.Token);
            TrackingBitmap obsoletePreviewBitmap = new(firstPath);
            DecodedImagePreview obsoletePreview = new(
                obsoletePreviewBitmap,
                obsoletePreviewBitmap.PixelSize);
            previewLoader.Complete(firstItem, obsoletePreview);
            await uiDispatcher.WaitForPendingAsync(timeout.Token);

            session.Navigate(1);

            await previewLoader.WaitUntilStartedAsync(
                secondItem,
                timeout.Token);
            uiDispatcher.RunNext();
            await obsoletePreviewBitmap.WaitForDisposalAsync(
                timeout.Token);

            obsoletePreviewBitmap.IsDisposed.Should().BeTrue();
            presentationSink.PreviewCount.Should().Be(0);
            presentationSink.FullResolutionCount.Should().Be(0);
        });
    }

    private static ImageLoadCoordinator CreateCoordinator(
        ImageViewerSession session,
        RecordingImageLoadPresentationSink presentationSink,
        RecordingViewerRenderFrameAwaiter frameAwaiter,
        bool isFastLoadingEnabled)
    {
        ImageFormatRegistry formatRegistry = new();

        return CreateCoordinator(
            session,
            presentationSink,
            frameAwaiter,
            new ImagePreviewLoader(
                formatRegistry,
                NullLogger<ImagePreviewLoader>.Instance),
            new FullResolutionImageLoader(formatRegistry),
            new AvaloniaViewerUiDispatcher(),
            isFastLoadingEnabled);
    }

    private static ImageLoadCoordinator CreateCoordinator(
        ImageViewerSession session,
        RecordingImageLoadPresentationSink presentationSink,
        RecordingViewerRenderFrameAwaiter frameAwaiter,
        IImagePreviewLoader previewLoader,
        IFullResolutionImageLoader fullResolutionLoader,
        IViewerUiDispatcher uiDispatcher,
        bool isFastLoadingEnabled)
    {
        return new ImageLoadCoordinator(
            session,
            previewLoader,
            fullResolutionLoader,
            presentationSink,
            frameAwaiter,
            uiDispatcher,
            NullLogger<ImageLoadCoordinator>.Instance,
            NullLogger<ImagePreviewPrefetcher>.Instance,
            isFastLoadingEnabled);
    }

    private static ImageViewerSession CreateSession(PicaImageItem item)
    {
        PicaViewerRequest request = new(
            new List<PicaImageItem> { item },
            item.Id);

        return new ImageViewerSession(request, true);
    }

    private static ImageViewerSession CreateSession(
        IReadOnlyList<PicaImageItem> items,
        Guid selectedItemId)
    {
        PicaViewerRequest request = new(
            items,
            selectedItemId);

        return new ImageViewerSession(request, true);
    }

    private static async Task<string> CreateImageAsync(
        string directoryPath,
        string fileName = "image.png")
    {
        string imagePath = Path.Combine(directoryPath, fileName);
        using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
        byte[] content = await new PngImageEncoder().EncodeAsync(
            bitmap,
            CancellationToken.None);
        await File.WriteAllBytesAsync(imagePath, content);

        return imagePath;
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageLoadCoordinatorTests),
            SessionLock,
            action);
    }
}

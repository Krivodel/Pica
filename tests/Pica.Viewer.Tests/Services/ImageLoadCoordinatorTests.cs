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

    private static ImageLoadCoordinator CreateCoordinator(
        ImageViewerSession session,
        RecordingImageLoadPresentationSink presentationSink,
        RecordingViewerRenderFrameAwaiter frameAwaiter,
        bool isFastLoadingEnabled)
    {
        ImageFormatRegistry formatRegistry = new();

        return new ImageLoadCoordinator(
            session,
            new ImagePreviewLoader(
                formatRegistry,
                NullLogger<ImagePreviewLoader>.Instance),
            new FullResolutionImageLoader(formatRegistry),
            presentationSink,
            frameAwaiter,
            new AvaloniaViewerUiDispatcher(),
            NullLogger<ImageLoadCoordinator>.Instance,
            NullLogger<ImagePreviewPrefetcher>.Instance,
            isFastLoadingEnabled);
    }

    private static ImageViewerSession CreateSession(PicaImageItem item)
    {
        PicaViewerRequest request = new(
            new List<PicaImageItem> { item },
            item.Id,
            new List<PicaActionDefinition>(),
            null);

        return new ImageViewerSession(request, true);
    }

    private static async Task<string> CreateImageAsync(
        string directoryPath)
    {
        string imagePath = Path.Combine(directoryPath, "image.png");
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

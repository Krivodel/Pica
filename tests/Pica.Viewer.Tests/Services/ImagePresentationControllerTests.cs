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

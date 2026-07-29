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

    private static ImageViewerSession CreateSession()
    {
        PicaImageItem item = new(
            ItemId,
            "image.png",
            "image.png");
        PicaViewerRequest request = new(
            new PicaImageItem[] { item },
            ItemId,
            Array.Empty<PicaActionDefinition>(),
            null);

        return new ImageViewerSession(request, true);
    }
}

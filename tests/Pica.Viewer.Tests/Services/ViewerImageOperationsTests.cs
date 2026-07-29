using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerImageOperationsTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task SaveCurrentAsync_WithSourceImage_PreservesContentAndFileName()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.webp");
            byte[] sourceContent = [10, 20, 30, 40];
            await File.WriteAllBytesAsync(sourcePath, sourceContent);
            PicaImageItem item = new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                sourcePath,
                "source.webp");
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);

            await operations.SaveCurrentAsync(
                item,
                CancellationToken.None);

            storageProvider.SuggestedFileName.Should().Be("source.webp");
            storageProvider.Destination.Content.Should().Equal(sourceContent);
        });
    }

    [Fact]
    public async Task SaveBitmapAsync_WithChannelImage_WritesNamedPng()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

            await operations.SaveBitmapAsync(
                bitmap,
                "source-R.png",
                CancellationToken.None);

            storageProvider.SuggestedFileName.Should().Be("source-R.png");
            AssertPngContent(storageProvider.Destination.Content);
        });
    }

    [Fact]
    public async Task SavePreparedSelectionAsync_WithSelectedDestination_WritesPngOnUiCaller()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            PreparedClipboardImage image =
                await new ClipboardImagePreparer().PrepareImageAsync(
                    bitmap,
                    CancellationToken.None);
            bool wasSaved = false;

            await operations.SavePreparedSelectionAsync(
                image,
                () => wasSaved = true,
                CancellationToken.None);
            Dispatcher.UIThread.VerifyAccess();

            wasSaved.Should().BeTrue();
            storageProvider.SuggestedFileName.Should().Be("selection.png");
            AssertPngContent(storageProvider.Destination.Content);
        });
    }

    [Fact]
    public async Task SavePreparedSelectionAsync_FromWorkerThread_OpensPickerOnUiThread()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            PreparedClipboardImage image =
                await new ClipboardImagePreparer().PrepareImageAsync(
                    bitmap,
                    CancellationToken.None);

            await Task.Run(() => operations.SavePreparedSelectionAsync(
                image,
                () => { },
                CancellationToken.None));

            storageProvider.SavePickerHasUiThreadAccess.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SavePreparedSelectionAsync_WhenPickerCanceled_DoesNotWrite()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            storageProvider.CancelSave();
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            PreparedClipboardImage image =
                await new ClipboardImagePreparer().PrepareImageAsync(
                    bitmap,
                    CancellationToken.None);
            bool wasSaved = false;

            await operations.SavePreparedSelectionAsync(
                image,
                () => wasSaved = true,
                CancellationToken.None);

            wasSaved.Should().BeFalse();
            storageProvider.Destination.Content.Should().BeEmpty();
        });
    }

    private static ViewerImageOperations CreateOperations(
        IStorageProvider storageProvider)
    {
        ViewerWindowPlatformContext platformContext = new(
            storageProvider,
            null);
        IViewerFilePickerService filePickerService =
            new AvaloniaViewerFilePickerService(
                new AvaloniaViewerUiDispatcher(),
                platformContext);

        return new ViewerImageOperations(
            new NullViewerClipboardWriter(),
            filePickerService,
            new ImageFormatRegistry(),
            new PngImageEncoder(),
            new RecordingViewerActionDispatcher());
    }

    private static void AssertPngContent(byte[] content)
    {
        content.Should().NotBeEmpty();
        content
            .Take(8)
            .Should()
            .Equal(137, 80, 78, 71, 13, 10, 26, 10);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageOperationsTests),
            SessionLock,
            action);
    }
}

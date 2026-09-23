using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAssertions;
using ImageMagick;
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
            storageProvider.SaveOptions?.FileTypeChoices?
                .SelectMany(fileType => fileType.Patterns ?? Array.Empty<string>())
                .Should().OnlyHaveUniqueItems();
            storageProvider.SaveOptions?.FileTypeChoices?[0].Patterns
                .Should().ContainSingle().Which.Should().Be("*.webp");
            storageProvider.SaveOptions?.SuggestedFileType
                .Should().BeSameAs(storageProvider.SaveOptions?.FileTypeChoices?[0]);
            storageProvider.Destination.Content.Should().Equal(sourceContent);
        });
    }

    [Fact]
    public async Task SaveCurrentAsync_WithCursorSource_PreservesOriginalFormatByDefault()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.cur");
            byte[] sourceContent = [0, 0, 2, 0];
            await File.WriteAllBytesAsync(sourcePath, sourceContent);
            PicaImageItem item = new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                sourcePath,
                "source.cur");
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);

            await operations.SaveCurrentAsync(item, CancellationToken.None);

            storageProvider.SaveOptions?.FileTypeChoices?[0].Patterns
                .Should().ContainSingle().Which.Should().Be("*.cur");
            storageProvider.SaveOptions?.FileTypeChoices?
                .SelectMany(fileType => fileType.Patterns ?? Array.Empty<string>())
                .Should().ContainSingle(pattern => pattern == "*.cur");
            storageProvider.Destination.Content.Should().Equal(sourceContent);
        });
    }

    [Fact]
    public async Task SaveCurrentAsync_WithJpegDestination_ConvertsPngContent()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.png");
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            byte[] sourceContent = await new PngImageEncoder().EncodeAsync(
                bitmap,
                CancellationToken.None);
            await File.WriteAllBytesAsync(sourcePath, sourceContent);
            PicaImageItem item = new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                sourcePath,
                "source.png");
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "converted.jpg"
            };
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);

            await operations.SaveCurrentAsync(item, CancellationToken.None);

            storageProvider.SaveOptions?.FileTypeChoices?
                .SelectMany(fileType => fileType.Patterns ?? Array.Empty<string>())
                .Should().Contain("*.jpg");
            storageProvider.Destination.Content.Take(3)
                .Should().Equal(255, 216, 255);
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
    public async Task SaveBitmapAsync_WithNonPngSuggestedName_DefaultsToPng()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new();
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

            await operations.SaveBitmapAsync(
                bitmap,
                "source.webp",
                CancellationToken.None);

            storageProvider.SuggestedFileName.Should().Be("source.png");
            storageProvider.SaveOptions?.SuggestedFileType
                .Should().BeSameAs(storageProvider.SaveOptions?.FileTypeChoices?[0]);
            AssertPngContent(storageProvider.Destination.Content);
        });
    }

    [Fact]
    public async Task SaveBitmapAsync_WithWebpDestination_WritesWebp()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "source-R.webp"
            };
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

            await operations.SaveBitmapAsync(
                bitmap,
                "source-R.png",
                CancellationToken.None);

            storageProvider.SaveOptions?.FileTypeChoices?[0].Patterns
                .Should().ContainSingle().Which.Should().Be("*.png");
            storageProvider.Destination.Content.Take(4)
                .Should().Equal((byte)'R', (byte)'I', (byte)'F', (byte)'F');
            storageProvider.Destination.Content.Skip(8).Take(4)
                .Should().Equal((byte)'W', (byte)'E', (byte)'B', (byte)'P');
        });
    }

    [Fact]
    public async Task SaveBitmapAsync_WithEveryOfferedFormat_WritesReadableImage()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            IReadOnlyList<string> extensions =
                new ImageFormatRegistry().GetWritableExtensions();

            foreach (string extension in extensions)
            {
                using RecordingStorageProvider storageProvider = new()
                {
                    SelectedFileName = "converted" + extension
                };
                ViewerImageOperations operations = CreateOperations(
                    storageProvider.Provider);

                await operations.SaveBitmapAsync(
                    bitmap,
                    "source.png",
                    CancellationToken.None);

                byte[] content = storageProvider.Destination.Content;
                content.Should().NotBeEmpty($"{extension} must contain encoded bytes");

                Exception? decodeError = Record.Exception(() =>
                {
                    MagickReadSettings settings = new()
                    {
                        Format = extension is ".ico" or ".cur"
                            ? MagickFormat.Ico
                            : MagickFormat.Unknown
                    };
                    using MagickImage savedImage = new(content, settings);
                    savedImage.Width.Should().BeGreaterThan(0);
                    savedImage.Height.Should().BeGreaterThan(0);
                });
                decodeError.Should().BeNull(
                    $"{extension} must produce a readable image; header: "
                    + Convert.ToHexString(content.AsSpan(0, Math.Min(16, content.Length))));
            }
        });
    }

    [Fact]
    public async Task SaveBitmapAsync_WithUnsupportedDestination_DoesNotWriteImage()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "source.txt"
            };
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);
            using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

            Func<Task> save = () => operations.SaveBitmapAsync(
                bitmap,
                "source.png",
                CancellationToken.None);

            await save.Should().ThrowAsync<NotSupportedException>();
            storageProvider.Destination.Content.Should().BeEmpty();
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
    public async Task SavePreparedSelectionAsync_WithJpegDestination_WritesJpeg()
    {
        await DispatchAsync(async () =>
        {
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "selection.jpeg"
            };
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

            wasSaved.Should().BeTrue();
            storageProvider.Destination.Content.Take(3)
                .Should().Equal(255, 216, 255);
        });
    }

    [Fact]
    public async Task SaveCurrentAsync_WithAnimatedWebpDestination_PreservesFrames()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.gif");
            AnimatedImageTestData.Create(sourcePath, MagickFormat.Gif);
            PicaImageItem item = new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                sourcePath,
                "source.gif");
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "converted.webp"
            };
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);

            await operations.SaveCurrentAsync(item, CancellationToken.None);

            using MagickImageCollection savedImages = new();
            savedImages.Read(storageProvider.Destination.Content);
            savedImages.Count.Should().Be(2);
        });
    }

    [Fact]
    public async Task SaveCurrentAsync_WithIconSource_ConvertsToPng()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.ico");
            using (MagickImage icon = new(MagickColors.Red, 16, 16))
            {
                icon.Write(sourcePath, MagickFormat.Ico);
            }

            PicaImageItem item = new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                sourcePath,
                "source.ico");
            using RecordingStorageProvider storageProvider = new()
            {
                SelectedFileName = "converted.png"
            };
            ViewerImageOperations operations = CreateOperations(
                storageProvider.Provider);

            await operations.SaveCurrentAsync(item, CancellationToken.None);

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

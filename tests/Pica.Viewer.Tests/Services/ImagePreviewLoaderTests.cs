using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

using Pica.Tests.Common;
using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImagePreviewLoaderTests
{
    private const int SourceWidth = 400;
    private const int SourceHeight = 200;
    private const string SourceFileName = "source.png";

    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task LoadAsync_WithProvidedPreview_UsesPreviewAndPreservesSourceDimensions()
    {
        await DispatchAsync(async () =>
        {
            using ImagePreviewTestContext context = new();
            context.AddProvidedPreview(32, 16);

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            AssertSourcePixelSize(preview);
            preview.Bitmap.Dispose();
        });
    }

    [Fact]
    public async Task LoadAsync_WithoutProvidedPreview_DecodesSmallPreviewWithoutCreatingFiles()
    {
        await DispatchAsync(async () =>
        {
            using ImagePreviewTestContext context = new();

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            AssertSourcePixelSize(preview);
            ImagePreviewLoader.PreviewDecodeWidth.Should().Be(128);
            Directory.GetFiles(context.DirectoryPath)
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .Be(context.SourcePath);
            preview.Bitmap.Dispose();
        });
    }

    [Theory]
    [InlineData(
        PicaImageFormats.AvifExtension,
        AvifImageTestData.Width,
        AvifImageTestData.Height)]
    [InlineData(
        PicaImageFormats.HeicExtension,
        HeicImageTestData.Width,
        HeicImageTestData.Height)]
    [InlineData(
        PicaImageFormats.HeifExtension,
        HeicImageTestData.Width,
        HeicImageTestData.Height)]
    public async Task LoadAsync_WithHeifFamilySource_DecodesPreview(
        string extension,
        int expectedWidth,
        int expectedHeight)
    {
        await DispatchAsync(async () =>
        {
            using ImagePreviewTestContext context = new(extension);

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            preview.SourcePixelSize.Should().Be(
                new PixelSize(expectedWidth, expectedHeight));
            preview.Bitmap.PixelSize.Width.Should().BePositive();
            preview.Bitmap.PixelSize.Height.Should().BePositive();
            preview.Bitmap.Dispose();
        });
    }

    [Theory]
    [InlineData(
        PicaImageFormats.AvifExtension,
        AvifImageTestData.Width,
        AvifImageTestData.Height)]
    [InlineData(
        PicaImageFormats.HeicExtension,
        HeicImageTestData.Width,
        HeicImageTestData.Height)]
    [InlineData(
        PicaImageFormats.HeifExtension,
        HeicImageTestData.Width,
        HeicImageTestData.Height)]
    public async Task FullResolutionLoadAsync_WithHeifFamilySource_DecodesBitmap(
        string extension,
        int expectedWidth,
        int expectedHeight)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                Path.ChangeExtension(SourceFileName, extension));
            CreateHeifFamilyImage(imagePath, extension);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().ContainSingle();
            image.Frames[0].Bitmap.PixelSize.Should().Be(
                new PixelSize(expectedWidth, expectedHeight));
        });
    }

    [Fact]
    public async Task LoadAsync_WithMultiPageTiffSource_DecodesFirstPagePreview()
    {
        await DispatchAsync(async () =>
        {
            using ImagePreviewTestContext context = new(PicaImageFormats.TiffExtension);

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            preview.SourcePixelSize.Should().Be(
                new PixelSize(TiffImageTestData.Width, TiffImageTestData.Height));
            preview.Bitmap.PixelSize.Width.Should().BePositive();
            preview.Bitmap.PixelSize.Height.Should().BePositive();
            preview.Bitmap.Dispose();
        });
    }

    [Fact]
    public async Task LoadAsync_WithMultiImageIcon_UsesLargestImageDimensions()
    {
        await DispatchAsync(async () =>
        {
            string imagePath = Path.Combine(
                AppContext.BaseDirectory,
                "AppIcon.ico");
            PicaImageItem item = new(
                ItemId,
                imagePath,
                Path.GetFileName(imagePath));
            ImagePreviewLoader loader = new(
                new ImageFormatRegistry(),
                NullLogger<ImagePreviewLoader>.Instance);

            DecodedImagePreview preview = await loader.LoadAsync(
                item,
                CancellationToken.None);

            preview.SourcePixelSize.Should().Be(
                new PixelSize(256, 256));
            preview.Bitmap.Dispose();
        });
    }

    [Fact]
    public async Task FullResolutionLoadAsync_WithMultiPageTiffSource_DecodesAllPages()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "source.tiff");
            TiffImageTestData.Create(imagePath);
            FullResolutionImageLoader loader = new(
                new ImageFormatRegistry(),
                MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);
            DecodedImage image = content.Groups[
                content.InitialGroupIndex].GetRequiredImage();

            image.Frames.Should().HaveCount(2);
            image.Frames[0].Bitmap.PixelSize.Should().Be(
                new PixelSize(TiffImageTestData.Width, TiffImageTestData.Height));
            image.FramePresentationMode.Should().Be(
                ImageFramePresentationModes.ManualNavigation);
        });
    }

    [Fact]
    public async Task LoadAsync_WhileDecoderIsRunning_ReleasesSourceFile()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(
                temporaryDirectory.DirectoryPath,
                SourceFileName);
            await File.WriteAllBytesAsync(
                sourcePath,
                new byte[] { 1, 2, 3, 4 });
            Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
            using BlockingImageDecoder decoder = new(bitmap);
            ImagePreviewLoader loader = new(
                new FixedImageDecoderResolver(decoder),
                NullLogger<ImagePreviewLoader>.Instance);
            PicaImageItem item = new(
                ItemId,
                sourcePath,
                SourceFileName);
            Task<DecodedImagePreview> loadingTask = loader.LoadAsync(
                item,
                CancellationToken.None);
            await decoder.OperationStarted;

            try
            {
                using FileStream exclusiveStream = new(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                exclusiveStream.CanRead.Should().BeTrue();
            }
            finally
            {
                decoder.Release();
            }

            DecodedImagePreview preview = await loadingTask;
            preview.Bitmap.Dispose();
        });
    }

    private static void AssertSourcePixelSize(DecodedImagePreview preview)
    {
        preview.SourcePixelSize.Should().Be(new PixelSize(SourceWidth, SourceHeight));
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePreviewLoaderTests),
            SessionLock,
            action);
    }

    private static void CreatePng(string path, int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to create the test image.");
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static void CreateHeifFamilyImage(string path, string extension)
    {
        if (string.Equals(
            extension,
            PicaImageFormats.AvifExtension,
            StringComparison.Ordinal))
        {
            AvifImageTestData.Create(path);
            return;
        }

        HeicImageTestData.Create(path);
    }

    private sealed class ImagePreviewTestContext : IDisposable
    {
        public string DirectoryPath => _temporaryDirectory.DirectoryPath;
        public string SourcePath { get; }

        private readonly PicaTemporaryDirectory _temporaryDirectory;
        private readonly ImagePreviewLoader _loader;
        private string? _previewPath;

        public ImagePreviewTestContext(string extension = ".png")
        {
            _temporaryDirectory = new PicaTemporaryDirectory();
            _loader = new ImagePreviewLoader(
                new ImageFormatRegistry(),
                NullLogger<ImagePreviewLoader>.Instance);
            SourcePath = Path.Combine(
                DirectoryPath,
                Path.ChangeExtension(SourceFileName, extension));

            if (extension is PicaImageFormats.AvifExtension
                or PicaImageFormats.HeicExtension
                or PicaImageFormats.HeifExtension)
            {
                CreateHeifFamilyImage(SourcePath, extension);
            }
            else if (string.Equals(
                extension,
                PicaImageFormats.TiffExtension,
                StringComparison.Ordinal))
            {
                TiffImageTestData.Create(SourcePath);
            }
            else
            {
                CreatePng(SourcePath, SourceWidth, SourceHeight);
            }
        }

        public void AddProvidedPreview(int width, int height)
        {
            _previewPath = Path.Combine(DirectoryPath, "provided.png");
            CreatePng(_previewPath, width, height);
        }

        public async Task<DecodedImagePreview> LoadAsync(CancellationToken ct)
        {
            PicaImageItem item = _previewPath is null
                ? new PicaImageItem(ItemId, SourcePath, Path.GetFileName(SourcePath))
                : new PicaImageItem(
                    ItemId,
                    SourcePath,
                    Path.GetFileName(SourcePath),
                    _previewPath);

            return await _loader.LoadAsync(item, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _temporaryDirectory.Dispose();
        }
    }
}

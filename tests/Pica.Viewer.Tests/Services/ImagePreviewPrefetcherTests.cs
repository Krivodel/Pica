using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImagePreviewPrefetcherTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly Guid FirstItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdItemId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task PrefetchAdjacentAsync_WithNeighboringImages_CachesBothNeighbors()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            IReadOnlyList<PicaImageItem> items =
                await CreateImageItemsAsync(
                    temporaryDirectory.DirectoryPath);
            PicaViewerRequest request = new(
                items,
                FirstItemId,
                new List<PicaActionDefinition>(),
                null);
            ImageViewerSession session = new(request, true);
            ImageFormatRegistry formatRegistry = new();
            using ImagePreviewPrefetcher prefetcher = new(
                session,
                new ImagePreviewLoader(
                    formatRegistry,
                    NullLogger<ImagePreviewLoader>.Instance),
                NullLogger<ImagePreviewPrefetcher>.Instance);

            await prefetcher.PrefetchAdjacentAsync(
                0,
                () => true,
                CancellationToken.None);

            DecodedImagePreview? nextPreview =
                prefetcher.Take(Path.GetFullPath(items[1].FilePath));
            DecodedImagePreview? previousPreview =
                prefetcher.Take(Path.GetFullPath(items[2].FilePath));
            nextPreview.Should().NotBeNull();
            previousPreview.Should().NotBeNull();
            nextPreview?.Bitmap.Dispose();
            previousPreview?.Bitmap.Dispose();
        });
    }

    private static async Task<IReadOnlyList<PicaImageItem>>
        CreateImageItemsAsync(string directoryPath)
    {
        string firstPath = Path.Combine(directoryPath, "first.png");
        string secondPath = Path.Combine(directoryPath, "second.png");
        string thirdPath = Path.Combine(directoryPath, "third.png");
        using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();
        byte[] content = await new PngImageEncoder().EncodeAsync(
            bitmap,
            CancellationToken.None);
        await File.WriteAllBytesAsync(firstPath, content);
        await File.WriteAllBytesAsync(secondPath, content);
        await File.WriteAllBytesAsync(thirdPath, content);

        return new List<PicaImageItem>
        {
            new(FirstItemId, firstPath, "first.png"),
            new(SecondItemId, secondPath, "second.png"),
            new(ThirdItemId, thirdPath, "third.png")
        };
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePreviewPrefetcherTests),
            SessionLock,
            action);
    }
}

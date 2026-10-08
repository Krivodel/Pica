using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Input;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class PngPlatformClipboardImageWriterTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return SkiaViewerTestSession.BuildAvaloniaApp();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetImageAsync_WithPreparedPixels_WritesPngWithoutFileOrText(bool isEncoded)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(PngPlatformClipboardImageWriterTests), SessionLock, async () =>
            {
                PreparedBitmapPixels pixels = new(
                    new ImageDimensions(BgraBitmapTestData.Width, BgraBitmapTestData.Height),
                    BgraBitmapTestData.RowBytes, BgraBitmapTestData.Pixels);
                PreparedBitmapPixels image = isEncoded
                    ? new PreparedClipboardImage(pixels.Dimensions, pixels.RowBytes, pixels.BgraPixels,
                        PngImageEncoder.EncodePixels(pixels, CancellationToken.None))
                    : pixels;
                RecordingClipboard clipboard = new();
                ViewerWindowPlatformContext platformContext = new(null, clipboard.Clipboard);
                using AvaloniaClipboardDataWriter dataWriter = new(platformContext,
                    NullLogger<AvaloniaClipboardDataWriter>.Instance);
                PngPlatformClipboardImageWriter writer = new(dataWriter, PicaClipboardFormats.PngMime);

                await writer.SetImageAsync(image, CancellationToken.None);

                IAsyncDataTransfer data = clipboard.Data
                    ?? throw new InvalidOperationException("The clipboard image was not written.");
                DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(PicaClipboardFormats.PngMime);
                data.Formats.Should().ContainSingle().Which.Should().Be(format);
                byte[] content = await data.TryGetValueAsync(format)
                    ?? throw new InvalidOperationException("The clipboard PNG is unavailable.");
                using SKBitmap decoded = SKBitmap.Decode(content)
                    ?? throw new InvalidOperationException("The clipboard PNG is invalid.");
                decoded.Width.Should().Be(BgraBitmapTestData.Width);
                decoded.Height.Should().Be(BgraBitmapTestData.Height);
                decoded.GetPixel(0, 0).Should().Be(new SKColor(32, 21, 11, 255));
                decoded.GetPixel(1, 1).Should().Be(new SKColor(34, 22, 12, 255));
            });
    }
}

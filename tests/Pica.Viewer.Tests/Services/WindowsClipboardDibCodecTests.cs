using System.Buffers.Binary;

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
public sealed class WindowsClipboardDibCodecTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public async Task CreateBitmapFile_WithDibV5_PreservesDimensionsColorsAndTransparency()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(WindowsClipboardDibCodecTests), SessionLock, async () =>
        {
            byte[] pixels = BgraBitmapTestData.Pixels;
            pixels[0] = 0;
            pixels[1] = 0;
            pixels[2] = 255;
            pixels[3] = 128;
            PreparedBitmapPixels prepared = new(new ImageDimensions(2, 2), BgraBitmapTestData.RowBytes, pixels);
            byte[] dib = WindowsDibV5Builder.Build(prepared);
            byte[] bmp = WindowsClipboardDibCodec.CreateBitmapFile(dib);
            ImageFormatRegistry registry = new();
            FullResolutionImageLoader loader = new(registry, MultiFrameImageDecoderTestFactory.Create());

            using DecodedImageContent content = await loader.LoadAsync(bmp, "clipboard.bmp", CancellationToken.None);
            DecodedImage image = content.Groups[0].GetRequiredImage();
            Bitmap bitmap = image.GetFrame(0)?.Bitmap ?? throw new InvalidOperationException("The first frame is unavailable.");
            PreparedBitmapPixels decoded = BitmapPixelReader.ReadUnpremultiplied(bitmap, CancellationToken.None);

            bitmap.PixelSize.Should().Be(BgraBitmapTestData.PixelSize);
            decoded.BgraPixels[3].Should().Be(pixels[3]);
            decoded.BgraPixels.Skip(4).Should().Equal(pixels.Skip(4));

            for (int channel = 0; channel < 3; channel++)
            {
                ((int)decoded.BgraPixels[channel]).Should().BeInRange(pixels[channel] - 1, pixels[channel] + 1);
            }
            BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(10, 4)).Should().Be(14 + WindowsDibV5Builder.HeaderSize);
        });
    }

    [Fact]
    public void CreateBitmapFile_WithIncompleteHeader_RejectsData()
    {
        Action decode = () => WindowsClipboardDibCodec.CreateBitmapFile(new byte[] { 1, 2, 3 });

        decode.Should().Throw<InvalidDataException>();
    }
}

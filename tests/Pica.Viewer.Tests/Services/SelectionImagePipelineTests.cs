using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Platform;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class SelectionImagePipelineTests
{
    [Fact]
    public void NormalizeSourceRect_WithEmptyRect_ReturnsNull()
    {
        PixelSize sourceSize = new(100, 50);
        PixelRect sourceRect = new();

        PixelRect? normalizedRect = BitmapPixelCopy.NormalizeSourceRect(sourceSize, sourceRect);

        normalizedRect.Should().BeNull();
    }

    [Fact]
    public void NormalizeSourceRect_WithPartiallyOutsideRect_ClampsToSource()
    {
        PixelSize sourceSize = new(100, 50);
        PixelRect sourceRect = new(-10, 20, 30, 40);

        PixelRect? normalizedRect = BitmapPixelCopy.NormalizeSourceRect(sourceSize, sourceRect);

        normalizedRect.Should().Be(new PixelRect(0, 20, 20, 30));
    }

    [Fact]
    public void EncodePixels_WithBgraPixels_PreservesPixelValues()
    {
        byte[] pixels = BgraBitmapTestData.Pixels;
        GCHandle pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        byte[] content;

        try
        {
            content = PngImageEncoder.EncodePixels(
                BgraBitmapTestData.PixelSize,
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                pixelsHandle.AddrOfPinnedObject(),
                BgraBitmapTestData.RowBytes,
                CancellationToken.None);
        }
        finally
        {
            pixelsHandle.Free();
        }

        using SKBitmap decoded = SKBitmap.Decode(content)
            ?? throw new InvalidOperationException("Failed to decode the test PNG.");
        decoded.Width.Should().Be(BgraBitmapTestData.Width);
        decoded.Height.Should().Be(BgraBitmapTestData.Height);
        decoded.GetPixel(0, 0).Should().Be(new SKColor(32, 21, 11, 255));
        decoded.GetPixel(1, 1).Should().Be(new SKColor(34, 22, 12, 255));
    }

}

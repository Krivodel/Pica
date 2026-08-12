using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageChannelBitmapLoaderTests
{
    private const int RowBytes = 8;

    [Fact]
    public void ApplyChannel_WithRedChannel_CreatesOpaqueGrayscalePixels()
    {
        PreparedBitmapPixels pixels = CreatePixels();

        ImageChannelBitmapLoader.ApplyChannel(
            pixels,
            ImageChannel.Red,
            CancellationToken.None);

        pixels.BgraPixels.Should().Equal(
            30, 30, 30, 255,
            60, 60, 60, 255);
    }

    [Fact]
    public void ApplyChannel_WithAlphaChannel_CreatesOpaqueAlphaVisualization()
    {
        PreparedBitmapPixels pixels = CreatePixels();

        ImageChannelBitmapLoader.ApplyChannel(
            pixels,
            ImageChannel.Alpha,
            CancellationToken.None);

        pixels.BgraPixels.Should().Equal(
            128, 128, 128, 255,
            255, 255, 255, 255);
    }

    [Fact]
    public async Task ReadHasAlphaAsync_WhileDecoderIsRunning_ReleasesSourceFile()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string sourcePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "source.png");
        await File.WriteAllBytesAsync(
            sourcePath,
            new byte[] { 1, 2, 3, 4 });
        using BlockingAlphaImageDecoder decoder = new();
        ImageChannelBitmapLoader loader = new(
            new FixedImageDecoderResolver(decoder));
        Task<bool> loadingTask = loader.ReadHasAlphaAsync(
            sourcePath,
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

        bool hasAlpha = await loadingTask;
        hasAlpha.Should().BeTrue();
    }

    private static PreparedBitmapPixels CreatePixels()
    {
        byte[] pixels =
        [
            10, 20, 30, 128,
            40, 50, 60, 255
        ];

        return new PreparedBitmapPixels(
            new ImageDimensions(2, 1),
            RowBytes,
            pixels);
    }
}

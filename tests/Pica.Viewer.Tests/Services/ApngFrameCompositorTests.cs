using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ApngFrameCompositorTests
{
    [Fact]
    public void Compose_WithTransparentSourcePixel_ClearsDestination()
    {
        ApngFrameCompositor compositor = new(2, 1);
        ApngFrameData firstFrame = CreateFrame(
            0,
            0,
            2,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        ApngFrameData secondFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        compositor.Compose(
            0,
            firstFrame,
            [
                0, 0, 255, 255,
                0, 255, 0, 255
            ],
            CancellationToken.None);

        byte[] result = compositor.Compose(
            1,
            secondFrame,
            [0, 0, 0, 0],
            CancellationToken.None);

        result.Should().Equal(
            0, 0, 0, 0,
            0, 255, 0, 255);
    }

    [Fact]
    public void Compose_WithTransparentOverPixel_PreservesDestination()
    {
        ApngFrameCompositor compositor = new(1, 1);
        ApngFrameData firstFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        ApngFrameData secondFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Over);
        compositor.Compose(
            0,
            firstFrame,
            [0, 0, 255, 255],
            CancellationToken.None);

        byte[] result = compositor.Compose(
            1,
            secondFrame,
            [0, 0, 0, 0],
            CancellationToken.None);

        result.Should().Equal(
            0, 0, 255, 255);
    }

    [Fact]
    public void Compose_WithPartiallyTransparentOverPixel_BlendsPremultipliedChannels()
    {
        ApngFrameCompositor compositor = new(1, 1);
        ApngFrameData firstFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        ApngFrameData secondFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Over);
        compositor.Compose(
            0,
            firstFrame,
            [0, 0, 255, 255],
            CancellationToken.None);

        byte[] result = compositor.Compose(
            1,
            secondFrame,
            [128, 0, 0, 128],
            CancellationToken.None);

        result.Should().Equal(
            128, 0, 127, 255);
    }

    [Fact]
    public void Compose_AfterBackgroundDisposal_ClearsPreviousFrameArea()
    {
        ApngFrameCompositor compositor = new(2, 1);
        ApngFrameData firstFrame = CreateFrame(
            0,
            0,
            2,
            1,
            ApngDisposeOperation.Background,
            ApngBlendOperation.Source);
        ApngFrameData secondFrame = CreateFrame(
            1,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        compositor.Compose(
            0,
            firstFrame,
            [
                0, 0, 255, 255,
                0, 0, 255, 255
            ],
            CancellationToken.None);

        byte[] result = compositor.Compose(
            1,
            secondFrame,
            [0, 255, 0, 255],
            CancellationToken.None);

        result.Should().Equal(
            0, 0, 0, 0,
            0, 255, 0, 255);
    }

    [Fact]
    public void Compose_AfterPreviousDisposal_RestoresSavedArea()
    {
        ApngFrameCompositor compositor = new(2, 1);
        ApngFrameData firstFrame = CreateFrame(
            0,
            0,
            2,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        ApngFrameData secondFrame = CreateFrame(
            0,
            0,
            1,
            1,
            ApngDisposeOperation.Previous,
            ApngBlendOperation.Source);
        ApngFrameData thirdFrame = CreateFrame(
            1,
            0,
            1,
            1,
            ApngDisposeOperation.None,
            ApngBlendOperation.Source);
        compositor.Compose(
            0,
            firstFrame,
            [
                0, 0, 255, 255,
                0, 0, 255, 255
            ],
            CancellationToken.None);
        compositor.Compose(
            1,
            secondFrame,
            [255, 0, 0, 255],
            CancellationToken.None);

        byte[] result = compositor.Compose(
            2,
            thirdFrame,
            [0, 255, 0, 255],
            CancellationToken.None);

        result.Should().Equal(
            0, 0, 255, 255,
            0, 255, 0, 255);
    }

    private static ApngFrameData CreateFrame(
        int x,
        int y,
        int width,
        int height,
        ApngDisposeOperation disposeOperation,
        ApngBlendOperation blendOperation)
    {
        return new ApngFrameData(
            x,
            y,
            width,
            height,
            TimeSpan.FromMilliseconds(100d),
            disposeOperation,
            blendOperation,
            new List<byte[]>().AsReadOnly());
    }
}

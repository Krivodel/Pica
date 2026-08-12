using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class AnimatedImageBitmapFaceTests
{
    [Fact]
    public void WriteBGRA_WithPartialArea_WritesRowsIntoCanvas()
    {
        AnimatedImageBitmapFace face = new(3, 3);
        byte[] pixels =
        [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16
        ];
        byte[] expected =
        [
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            1, 2, 3, 4,
            5, 6, 7, 8,
            0, 0, 0, 0,
            9, 10, 11, 12,
            13, 14, 15, 16
        ];

        face.WriteBGRA(pixels, 1, 1, 2, 2);

        face.CopyPixels().Should().Equal(expected);
    }

    [Fact]
    public void ReadBGRA_WithPartialArea_ReturnsContiguousRows()
    {
        AnimatedImageBitmapFace face = new(3, 3);
        byte[] pixels =
        [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16
        ];
        byte[] result = new byte[pixels.Length];
        face.WriteBGRA(pixels, 1, 1, 2, 2);

        face.ReadBGRA(result, 1, 1, 2, 2);

        result.Should().Equal(pixels);
    }

    [Fact]
    public void Clear_WithPartialArea_ClearsOnlySelectedPixels()
    {
        AnimatedImageBitmapFace face = new(3, 2);
        byte[] pixels =
        [
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16,
            17, 18, 19, 20,
            21, 22, 23, 24
        ];
        byte[] expected =
        [
            1, 2, 3, 4,
            0, 0, 0, 0,
            9, 10, 11, 12,
            13, 14, 15, 16,
            0, 0, 0, 0,
            21, 22, 23, 24
        ];
        face.WriteBGRA(pixels, 0, 0, 3, 2);

        face.Clear(1, 0, 1, 2);

        face.CopyPixels().Should().Equal(expected);
    }
}

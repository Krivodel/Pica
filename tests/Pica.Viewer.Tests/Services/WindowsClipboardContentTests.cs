using System.Buffers.Binary;
using System.Text;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsClipboardContentTests
{
    [Fact]
    public void Build_WithPreparedBitmap_CreatesTopDownDibV5WithExactPixels()
    {
        byte[] pixels = BgraBitmapTestData.Pixels;
        PreparedClipboardBitmap bitmap = new(
            BgraBitmapTestData.PixelSize,
            BgraBitmapTestData.RowBytes,
            pixels);

        byte[] content = WindowsDibV5Builder.Build(bitmap);

        BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(0, 4))
            .Should().Be(WindowsDibV5Builder.HeaderSize);
        BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(4, 4))
            .Should().Be(BgraBitmapTestData.Width);
        BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(8, 4))
            .Should().Be(-BgraBitmapTestData.Height);
        BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(14, 2)).Should().Be(32);
        content.AsSpan(WindowsDibV5Builder.HeaderSize).ToArray().Should().Equal(pixels);
    }

    [Fact]
    public void Build_WithFilePath_CreatesUnicodeFileDropContent()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            "pica-tests",
            "clipboard-photo.jpg");
        string expectedPath = Path.GetFullPath(filePath);

        byte[] content = WindowsDropFilesBuilder.Build(filePath);

        BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(0, 4))
            .Should().Be(WindowsDropFilesBuilder.HeaderSize);
        BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(16, 4)).Should().Be(1);
        Encoding.Unicode.GetString(content, WindowsDropFilesBuilder.HeaderSize,
                content.Length - WindowsDropFilesBuilder.HeaderSize)
            .Should().Be(expectedPath + "\0\0");
    }
}

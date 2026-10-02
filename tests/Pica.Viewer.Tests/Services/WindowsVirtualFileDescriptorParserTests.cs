using System.Text;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsVirtualFileDescriptorParserTests
{
    [Fact]
    public void Parse_WithUnicodeDescriptor_ReturnsNameAndSize()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "пример.png",
            Encoding.Unicode,
            declaredSize: 1234);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                WindowsFileDescriptorEncoding.Unicode,
                ClipboardImageLimits.MaximumCandidates);

        WindowsVirtualFileDescriptor descriptor = descriptors.Should()
            .ContainSingle()
            .Subject;
        descriptor.FileName.Should().Be("пример.png");
        descriptor.DeclaredSize.Should().Be(1234);
        descriptor.IsDirectory.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithAnsiDescriptor_ReturnsName()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.AnsiDescriptorSize,
            "image.png",
            Encoding.ASCII,
            declaredSize: null);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                WindowsFileDescriptorEncoding.Ansi,
                ClipboardImageLimits.MaximumCandidates);

        descriptors.Should().ContainSingle()
            .Which.FileName.Should().Be("image.png");
    }

    [Fact]
    public void Parse_WithDirectoryAttribute_MarksDirectory()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "folder",
            Encoding.Unicode,
            declaredSize: null,
            isDirectory: true);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                WindowsFileDescriptorEncoding.Unicode,
                ClipboardImageLimits.MaximumCandidates);

        descriptors.Should().ContainSingle()
            .Which.IsDirectory.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithIncompleteDescriptor_ThrowsInvalidDataException()
    {
        byte[] data = new byte[sizeof(uint) + 10];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data, 1);

        Action act = () => WindowsVirtualFileDescriptorParser.Parse(
            data,
            WindowsFileDescriptorEncoding.Unicode,
            ClipboardImageLimits.MaximumCandidates);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_WithExcessiveItemCount_ThrowsInvalidDataException()
    {
        byte[] data = new byte[sizeof(uint)];
        int maximumFileCount = ClipboardImageLimits.MaximumCandidates;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            data,
            checked((uint)(maximumFileCount + 1)));

        Action act = () => WindowsVirtualFileDescriptorParser.Parse(
            data,
            WindowsFileDescriptorEncoding.Unicode,
            maximumFileCount);

        act.Should().Throw<InvalidDataException>();
    }
}

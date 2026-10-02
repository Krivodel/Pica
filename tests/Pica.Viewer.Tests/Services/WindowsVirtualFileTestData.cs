using System.Buffers.Binary;
using System.Text;

namespace Pica.Viewer.Tests.Services;

internal static class WindowsVirtualFileTestData
{
    public const int AnsiDescriptorSize = 332;
    public const int UnicodeDescriptorSize = 592;

    private const int FileAttributesOffset = 36;
    private const int FileSizeHighOffset = 64;
    private const int FileSizeLowOffset = 68;
    private const int FileNameOffset = 72;

    public static byte[] CreateDescriptorGroup(
        int descriptorSize,
        string fileName,
        Encoding fileNameEncoding,
        ulong? declaredSize,
        bool isDirectory = false)
    {
        byte[] data = new byte[sizeof(uint) + descriptorSize];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        Span<byte> descriptor = data.AsSpan(sizeof(uint), descriptorSize);
        uint flags = 0;

        if (declaredSize.HasValue)
        {
            flags |= 0x00000040;
            BinaryPrimitives.WriteUInt32LittleEndian(
                descriptor[FileSizeHighOffset..],
                (uint)(declaredSize.Value >> 32));
            BinaryPrimitives.WriteUInt32LittleEndian(
                descriptor[FileSizeLowOffset..],
                (uint)declaredSize.Value);
        }

        if (isDirectory)
        {
            flags |= 0x00000004;
            BinaryPrimitives.WriteUInt32LittleEndian(
                descriptor[FileAttributesOffset..],
                0x00000010);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(descriptor, flags);
        byte[] nameBytes = fileNameEncoding.GetBytes(fileName);
        nameBytes.CopyTo(descriptor[FileNameOffset..]);

        return data;
    }
}

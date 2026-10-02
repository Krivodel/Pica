using System.Buffers.Binary;
using System.Text;

namespace Pica.Viewer.Services;

internal static class WindowsVirtualFileDescriptorParser
{
    private const int NameOffset = 72;
    private const int NameCapacity = 260;
    private const int AttributesOffset = 36;
    private const int SizeHighOffset = 64;
    private const int SizeLowOffset = 68;
    private const uint DirectoryAttribute = 0x10;
    private const uint HasAttributes = 0x04;
    private const uint HasSize = 0x40;

    public static IReadOnlyList<WindowsVirtualFileDescriptor> Parse(
        ReadOnlySpan<byte> data, WindowsFileDescriptorEncoding encoding, int maximumFileCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileCount, 1);

        if (data.Length < sizeof(uint))
        {
            throw new InvalidDataException("The virtual file descriptor is incomplete.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
        int nameBytes = NameCapacity * (encoding == WindowsFileDescriptorEncoding.Unicode ? sizeof(char) : sizeof(byte));
        int descriptorBytes = NameOffset + nameBytes;

        if ((count > maximumFileCount) || (sizeof(uint) + (long)count * descriptorBytes > data.Length))
        {
            throw new InvalidDataException("The virtual file descriptor count or content is invalid.");
        }

        Encoding nameEncoding = Encoding.Unicode;

        if (encoding == WindowsFileDescriptorEncoding.Ansi)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            nameEncoding = OperatingSystem.IsWindows()
                ? Encoding.GetEncoding((int)WindowsClipboardAccess.GetAnsiCodePage()) : Encoding.Latin1;
        }

        List<WindowsVirtualFileDescriptor> descriptors = [];

        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> descriptor = data.Slice(sizeof(uint) + index * descriptorBytes, descriptorBytes);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            uint attributes = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[AttributesOffset..]);
            ulong? declaredSize = (flags & HasSize) != 0
                ? ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(descriptor[SizeHighOffset..]) << 32)
                    | BinaryPrimitives.ReadUInt32LittleEndian(descriptor[SizeLowOffset..])
                : null;
            string name = nameEncoding.GetString(descriptor.Slice(NameOffset, nameBytes));
            int terminator = name.IndexOf('\0');

            descriptors.Add(new WindowsVirtualFileDescriptor(terminator < 0 ? name : name[..terminator],
                declaredSize, ((flags & HasAttributes) != 0) && ((attributes & DirectoryAttribute) != 0)));
        }

        return descriptors;
    }
}

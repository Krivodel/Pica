using System.Buffers.Binary;
using System.Text;

namespace Pica.Viewer.Services;

internal static class WindowsDropFilesBuilder
{
    internal const int HeaderSize = 20;

    private const int WideFileNamesOffset = 16;

    public static byte[] Build(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("The clipboard file path is required.", nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        byte[] fileNames = Encoding.Unicode.GetBytes(fullPath + "\0\0");
        byte[] content = new byte[checked(HeaderSize + fileNames.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(0, 4), HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(
            content.AsSpan(WideFileNamesOffset, 4),
            1);
        fileNames.CopyTo(content, HeaderSize);

        return content;
    }
}

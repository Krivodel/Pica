using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services;

internal static class WindowsStorageMediumReader
{
    public static bool TryReadGlobalMemory(
        IDataObject dataObject,
        short formatId,
        int maxBytes,
        string tooLargeMessage,
        out byte[] data)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooLargeMessage);
        FORMATETC format = CreateFormat(
            formatId,
            itemIndex: -1,
            TYMED.TYMED_HGLOBAL);

        if (dataObject.QueryGetData(ref format) != WindowsClipboardAccess.Succeeded)
        {
            data = Array.Empty<byte>();
            return false;
        }

        dataObject.GetData(ref format, out STGMEDIUM medium);

        try
        {
            if (medium.tymed != TYMED.TYMED_HGLOBAL
                || medium.unionmember == nint.Zero)
            {
                throw new InvalidDataException(
                    "The transferred data has an unsupported storage type.");
            }

            data = ReadGlobalMemory(
                medium.unionmember,
                maxBytes,
                tooLargeMessage);
            return true;
        }
        finally
        {
            WindowsClipboardAccess.ReleaseStgMedium(ref medium);
        }
    }

    [SupportedOSPlatform("windows")]
    public static byte[] ReadIndexedContent(
        IDataObject dataObject,
        short formatId,
        int itemIndex,
        int maxBytes,
        int bufferSize,
        string tooLargeMessage)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooLargeMessage);
        FORMATETC format = CreateFormat(
            formatId,
            itemIndex,
            TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL);
        dataObject.GetData(ref format, out STGMEDIUM medium);

        try
        {
            return medium.tymed switch
            {
                TYMED.TYMED_ISTREAM => ReadComStream(
                    medium.unionmember,
                    maxBytes,
                    bufferSize,
                    tooLargeMessage),
                TYMED.TYMED_HGLOBAL => ReadGlobalMemory(
                    medium.unionmember,
                    maxBytes,
                    tooLargeMessage),
                _ => throw new InvalidDataException(
                    "The transferred data has an unsupported storage type.")
            };
        }
        finally
        {
            WindowsClipboardAccess.ReleaseStgMedium(ref medium);
        }
    }

    internal static byte[] ReadGlobalMemory(
        nint memoryHandle,
        int maxBytes,
        string tooLargeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooLargeMessage);

        if (memoryHandle == nint.Zero)
        {
            throw new InvalidDataException(
                "The transferred memory is unavailable.");
        }

        nuint size = WindowsClipboardAccess.GlobalSize(memoryHandle);

        if (size > (nuint)maxBytes)
        {
            throw new InvalidDataException(tooLargeMessage);
        }

        if (size == 0)
        {
            return Array.Empty<byte>();
        }

        nint dataPointer = WindowsClipboardAccess.GlobalLock(memoryHandle);

        if (dataPointer == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            byte[] data = new byte[checked((int)size)];
            Marshal.Copy(dataPointer, data, 0, data.Length);
            return data;
        }
        finally
        {
            _ = WindowsClipboardAccess.GlobalUnlock(memoryHandle);
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ReadComStream(
        nint streamPointer,
        int maxBytes,
        int bufferSize,
        string tooLargeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        if (streamPointer == nint.Zero)
        {
            throw new InvalidDataException(
                "The transferred stream is unavailable.");
        }

        object streamObject = Marshal.GetObjectForIUnknown(streamPointer);

        try
        {
            if (streamObject is not IStream stream)
            {
                throw new InvalidDataException(
                    "The transferred stream is invalid.");
            }

            using MemoryStream output = new();
            byte[] buffer = new byte[bufferSize];
            nint bytesReadPointer = Marshal.AllocHGlobal(sizeof(int));

            try
            {
                while (true)
                {
                    Marshal.WriteInt32(bytesReadPointer, 0);
                    stream.Read(buffer, buffer.Length, bytesReadPointer);
                    int bytesRead = Marshal.ReadInt32(bytesReadPointer);

                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    if (bytesRead > buffer.Length)
                    {
                        throw new InvalidDataException(
                            "The transferred stream returned an invalid byte count.");
                    }

                    if (output.Length + bytesRead > maxBytes)
                    {
                        throw new InvalidDataException(tooLargeMessage);
                    }

                    output.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bytesReadPointer);
            }

            return output.ToArray();
        }
        finally
        {
            if (Marshal.IsComObject(streamObject))
            {
                _ = Marshal.ReleaseComObject(streamObject);
            }
        }
    }

    private static FORMATETC CreateFormat(
        short formatId,
        int itemIndex,
        TYMED storageType)
    {
        return new FORMATETC
        {
            cfFormat = formatId,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = itemIndex,
            ptd = nint.Zero,
            tymed = storageType
        };
    }
}

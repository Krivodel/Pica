using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

[SupportedOSPlatform("windows")]
internal sealed class ClipboardStorageDataObject : IDataObject
{
    internal nint AllocatedMemory { get; private set; }

    private readonly byte[] _content;
    private readonly TYMED _storageType;

    internal ClipboardStorageDataObject(byte[] content, TYMED storageType)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _storageType = storageType;
    }

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        if (_storageType == TYMED.TYMED_ISTREAM)
        {
            int result = CreateStreamOnHGlobal(nint.Zero, true, out IStream stream);
            Marshal.ThrowExceptionForHR(result);
            stream.Write(_content, _content.Length, nint.Zero);
            stream.Seek(0, 0, nint.Zero);
            Marshal.ThrowExceptionForHR(GetHGlobalFromStream(stream, out nint memory));
            AllocatedMemory = memory;
            nint streamPointer = Marshal.GetComInterfaceForObject(stream, typeof(IStream));
            Marshal.ReleaseComObject(stream);
            medium = new STGMEDIUM { tymed = _storageType, unionmember = streamPointer };
            return;
        }

        AllocatedMemory = WindowsClipboardAccess.GlobalAlloc(WindowsClipboardAccess.MoveableGlobalMemory, (nuint)_content.Length);
        nint address = WindowsClipboardAccess.GlobalLock(AllocatedMemory);

        try
        {
            Marshal.Copy(_content, 0, address, _content.Length);
        }
        finally
        {
            WindowsClipboardAccess.GlobalUnlock(AllocatedMemory);
        }

        medium = new STGMEDIUM { tymed = _storageType, unionmember = AllocatedMemory };
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
    {
        throw new NotSupportedException();
    }

    public int QueryGetData(ref FORMATETC format)
    {
        return WindowsClipboardAccess.Succeeded;
    }

    public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
    {
        formatOut = default;
        return Marshal.GetHRForException(new NotSupportedException());
    }

    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release)
    {
        throw new NotSupportedException();
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        throw new NotSupportedException();
    }

    public int DAdvise(ref FORMATETC format, ADVF advf, IAdviseSink adviseSink, out int connection)
    {
        connection = 0;
        return Marshal.GetHRForException(new NotSupportedException());
    }

    public void DUnadvise(int connection)
    {
        throw new NotSupportedException();
    }

    public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
    {
        enumAdvise = null;
        return Marshal.GetHRForException(new NotSupportedException());
    }

    [DllImport("ole32.dll")]
    private static extern int CreateStreamOnHGlobal(nint memory, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, out IStream stream);

    [DllImport("ole32.dll")]
    private static extern int GetHGlobalFromStream(IStream stream, out nint memory);
}

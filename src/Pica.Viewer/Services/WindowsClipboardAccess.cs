using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Pica.Viewer.Services;

internal static class WindowsClipboardAccess
{
    internal const int Succeeded = 0;
    internal const uint FileDropFormat = 15;
    internal const uint DibFormat = 8;
    internal const uint DibV5Format = 17;
    internal const uint UnicodeTextFormat = 13;
    internal const uint MoveableGlobalMemory = 0x0002;

    private const int OpenAttemptCount = 10;
    private const int RetryDelayMilliseconds = 50;
    private const string Ole32Library = "ole32.dll";

    internal static Task UseAsync(Action operation, CancellationToken ct)
    {
        return UseAsync(nint.Zero, operation, ct);
    }

    internal static async Task UseAsync(nint owner, Action operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        int lastError = 0;

        for (int attempt = 0; attempt < OpenAttemptCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (OpenClipboard(owner))
            {
                try
                {
                    operation();

                    return;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            lastError = Marshal.GetLastWin32Error();
            await Task.Delay(RetryDelayMilliseconds, ct).ConfigureAwait(false);
        }

        throw new Win32Exception(lastError, "The Windows clipboard could not be opened.");
    }

    internal static uint RegisterFormat(string name)
    {
        uint format = RegisterClipboardFormat(name);

        return format != 0 ? format
            : throw new Win32Exception(Marshal.GetLastWin32Error(), "A clipboard format could not be registered.");
    }

    internal static void Clear()
    {
        if (!EmptyClipboard())
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The Windows clipboard could not be cleared.");
        }
    }

    internal static void SetBytes(uint format, byte[] content)
    {
        nint memory = GlobalAlloc(MoveableGlobalMemory, checked((nuint)content.Length));

        if (memory == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        bool transferred = false;

        try
        {
            nint address = GlobalLock(memory);

            if (address == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                Marshal.Copy(content, 0, address, content.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (SetClipboardData(format, memory) == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            transferred = true;
        }
        finally
        {
            if (!transferred)
            {
                GlobalFree(memory);
            }
        }
    }

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    internal static extern nint GetClipboardData(uint format);

    [DllImport(WindowsNativeLibraryNames.User32)]
    internal static extern uint GetClipboardSequenceNumber();

    [DllImport(WindowsNativeLibraryNames.Kernel32, EntryPoint = "GetACP")]
    internal static extern uint GetAnsiCodePage();

    [DllImport(WindowsNativeLibraryNames.Shell32, EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    internal static extern uint DragQueryFile(nint handle, uint index, StringBuilder? path, uint length);

    [DllImport(Ole32Library)]
    internal static extern int OleInitialize(nint reserved);

    [DllImport(Ole32Library)]
    internal static extern void OleUninitialize();

    [DllImport(Ole32Library)]
    internal static extern int OleGetClipboard([MarshalAs(UnmanagedType.Interface)] out IDataObject? dataObject);

    [DllImport(Ole32Library)]
    internal static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    internal static extern nint GlobalLock(nint memory);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(nint memory);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    internal static extern nuint GlobalSize(nint memory);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    internal static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    internal static extern nint GlobalFree(nint memory);

    [DllImport(WindowsNativeLibraryNames.User32, EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    private static extern nint SetClipboardData(uint format, nint memory);
}

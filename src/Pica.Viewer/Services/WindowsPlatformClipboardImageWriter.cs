using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;

using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class WindowsPlatformClipboardImageWriter : IPlatformClipboardImageWriter
{
    private const int ClipboardOpenAttemptCount = 10;
    private const int ClipboardOpenRetryDelayMilliseconds = 50;
    private const uint MoveableGlobalMemory = 0x0002;
    private const uint FileDropClipboardFormat = 15;
    private const uint DibV5ClipboardFormat = 17;
    private const uint CopyDropEffect = 1;
    private const string PreferredDropEffectClipboardFormat = "Preferred DropEffect";

    private readonly AvaloniaClipboardDataWriter _clipboardDataWriter;
    private readonly ClipboardImagePreparer _imagePreparer;

    public WindowsPlatformClipboardImageWriter(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        ClipboardImagePreparer imagePreparer)
    {
        _clipboardDataWriter = clipboardDataWriter
            ?? throw new ArgumentNullException(nameof(clipboardDataWriter));
        _imagePreparer = imagePreparer
            ?? throw new ArgumentNullException(nameof(imagePreparer));
    }

    public async Task SetImageAsync(PreparedClipboardImage image, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        byte[] dibContent = WindowsDibV5Builder.Build(image);

        await WriteClipboardAsync(
            () =>
            {
                SetClipboardBytes(DibV5ClipboardFormat, dibContent);
                SetClipboardBytes(
                    RegisterClipboardFormat(PicaClipboardFormats.WindowsPng),
                    image.PngContent);
                SetClipboardBytes(
                    RegisterClipboardFormat(PicaClipboardFormats.PngMime),
                    image.PngContent);
            },
            ct).ConfigureAwait(false);
    }

    public async Task SetFileWithImageAsync(
        IStorageFile file,
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bitmap);

        string? filePath = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            await _clipboardDataWriter.SetFileAsync(file, ct).ConfigureAwait(false);
            return;
        }

        PreparedClipboardBitmap preparedBitmap = await _imagePreparer
            .PrepareBitmapAsync(bitmap, ct)
            .ConfigureAwait(false);
        byte[] dibContent = WindowsDibV5Builder.Build(preparedBitmap);
        byte[] fileDropContent = WindowsDropFilesBuilder.Build(filePath);
        byte[] preferredDropEffect = CreatePreferredDropEffect();

        await WriteClipboardAsync(
            () =>
            {
                SetClipboardBytes(DibV5ClipboardFormat, dibContent);
                SetClipboardBytes(FileDropClipboardFormat, fileDropContent);
                SetClipboardBytes(
                    RegisterClipboardFormat(PreferredDropEffectClipboardFormat),
                    preferredDropEffect);
            },
            ct).ConfigureAwait(false);
    }

    private async Task WriteClipboardAsync(
        Action writeContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(writeContent);

        await _clipboardDataWriter.ReleasePendingDataAsync(ct).ConfigureAwait(false);
        await OpenClipboardAsync(ct).ConfigureAwait(false);

        try
        {
            if (!EmptyClipboard())
            {
                throw CreateWin32Exception("Failed to clear the Windows clipboard.");
            }

            writeContent();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static byte[] CreatePreferredDropEffect()
    {
        byte[] content = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(content, CopyDropEffect);

        return content;
    }

    private static async Task OpenClipboardAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt < ClipboardOpenAttemptCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (OpenClipboard(IntPtr.Zero))
            {
                return;
            }

            await Task.Delay(ClipboardOpenRetryDelayMilliseconds, ct).ConfigureAwait(false);
        }

        throw CreateWin32Exception("Failed to open the Windows clipboard.");
    }

    private static uint RegisterClipboardFormat(string formatName)
    {
        uint format = RegisterClipboardFormatNative(formatName);

        if (format == 0)
        {
            throw CreateWin32Exception(
                $"Failed to register clipboard format '{formatName}'.");
        }

        return format;
    }

    private static void SetClipboardBytes(uint format, byte[] content)
    {
        IntPtr memory = GlobalAlloc(MoveableGlobalMemory, checked((nuint)content.Length));

        if (memory == IntPtr.Zero)
        {
            throw CreateWin32Exception("Failed to allocate memory for the Windows clipboard.");
        }

        bool isOwnershipTransferred = false;

        try
        {
            IntPtr address = GlobalLock(memory);

            if (address == IntPtr.Zero)
            {
                throw CreateWin32Exception("Failed to lock Windows clipboard memory.");
            }

            try
            {
                Marshal.Copy(content, 0, address, content.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (SetClipboardData(format, memory) == IntPtr.Zero)
            {
                throw CreateWin32Exception("Failed to write the image to the Windows clipboard.");
            }

            isOwnershipTransferred = true;
        }
        finally
        {
            if (!isOwnershipTransferred)
            {
                GlobalFree(memory);
            }
        }
    }

    private static Win32Exception CreateWin32Exception(string message)
    {
        return new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport(WindowsNativeLibraryNames.User32, EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormatNative(string formatName);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}

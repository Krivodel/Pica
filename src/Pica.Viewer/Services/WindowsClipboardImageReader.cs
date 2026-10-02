using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class WindowsClipboardImageReader : IPlatformClipboardSnapshotReader
{
    private readonly ClipboardImageFormatCatalog _formats;
    private readonly ILogger<WindowsClipboardImageReader> _logger;

    public WindowsClipboardImageReader(
        ClipboardImageFormatCatalog formats,
        ILogger<WindowsClipboardImageReader> logger)
    {
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClipboardDataSnapshot> ReadAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ClipboardDataSnapshot();
        }

        ClipboardDataSnapshot snapshot = await ReadVirtualFilesAsync(ct).ConfigureAwait(false);

        try
        {
            await WindowsClipboardAccess.UseAsync(() =>
            {
                if (snapshot.SequenceNumber != WindowsClipboardAccess.GetClipboardSequenceNumber())
                {
                    snapshot.Dispose();
                }

                ReadFormats(snapshot);
                snapshot.SequenceNumber = WindowsClipboardAccess.GetClipboardSequenceNumber();
            }, ct).ConfigureAwait(false);

            return snapshot;
        }
        catch (Exception)
        {
            snapshot.Dispose();
            throw;
        }
    }

    private static void ReadFiles(ClipboardDataSnapshot snapshot)
    {
        if (!WindowsClipboardAccess.IsClipboardFormatAvailable(WindowsClipboardAccess.FileDropFormat))
        {
            return;
        }

        nint handle = WindowsClipboardAccess.GetClipboardData(WindowsClipboardAccess.FileDropFormat);
        uint count = WindowsClipboardAccess.DragQueryFile(handle, uint.MaxValue, null, 0);

        for (uint index = 0; index < Math.Min(count, ClipboardImageLimits.MaximumCandidates); index++)
        {
            uint length = WindowsClipboardAccess.DragQueryFile(handle, index, null, 0);
            StringBuilder path = new(checked((int)length + 1));
            WindowsClipboardAccess.DragQueryFile(handle, index, path, checked(length + 1));

            if (path.Length > 0)
            {
                snapshot.Files.Add(ClipboardImageInput.FromFile(path.ToString()));
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<ClipboardDataSnapshot> ReadVirtualFilesAsync(CancellationToken ct)
    {
        TaskCompletionSource<ClipboardDataSnapshot> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellation = ct.Register(() => completion.TrySetCanceled(ct));
        Thread captureThread = new(() =>
        {
            ClipboardDataSnapshot snapshot = new();
            int initialization = WindowsClipboardAccess.OleInitialize(nint.Zero);

            try
            {
                ct.ThrowIfCancellationRequested();
                snapshot.SequenceNumber = WindowsClipboardAccess.GetClipboardSequenceNumber();

                if (initialization >= WindowsClipboardAccess.Succeeded)
                {
                    ReadVirtualFiles(snapshot, ct);
                }
                else
                {
                    _logger.LogWarning(new COMException("Clipboard OLE initialization failed.", initialization),
                        "Virtual clipboard files could not be captured");
                }

                if (!completion.TrySetResult(snapshot))
                {
                    snapshot.Dispose();
                }
            }
            catch (Exception ex)
            {
                snapshot.Dispose();

                if (!completion.TrySetException(ex) && (ex is not OperationCanceledException))
                {
                    _logger.LogDebug(ex, "Canceled clipboard capture finished with an error");
                }
            }
            finally
            {
                if (initialization >= WindowsClipboardAccess.Succeeded)
                {
                    WindowsClipboardAccess.OleUninitialize();
                }
            }
        })
        {
            IsBackground = true,
            Name = "Pica clipboard snapshot"
        };
        captureThread.SetApartmentState(ApartmentState.STA);
        captureThread.Start();

        return await completion.Task.ConfigureAwait(false);
    }

    private void ReadFormats(ClipboardDataSnapshot snapshot)
    {
        ReadFiles(snapshot);

        foreach (KeyValuePair<string, string> format in _formats.EncodedFormats)
        {
            uint identifier = WindowsClipboardAccess.RegisterFormat(format.Key);
            ReadFormat(identifier, bytes => snapshot.Images.Add(
                ClipboardImageInput.FromBytes($"clipboard{format.Value}", bytes)));
        }

        foreach (uint identifier in new uint[] { WindowsClipboardAccess.DibV5Format, WindowsClipboardAccess.DibFormat })
        {
            ReadFormat(identifier, bytes => snapshot.Rasters.Add(
                ClipboardImageInput.FromBytes("clipboard.bmp", WindowsClipboardDibCodec.CreateBitmapFile(bytes))));
        }

        ReadFormat(WindowsClipboardAccess.UnicodeTextFormat,
            bytes => snapshot.Text.Add(Encoding.Unicode.GetString(bytes).TrimEnd('\0')));
        foreach (KeyValuePair<string, Encoding> format in ClipboardImageLinkFormats.Formats)
        {
            ReadFormat(WindowsClipboardAccess.RegisterFormat(format.Key),
                bytes => snapshot.Text.Add(format.Value.GetString(bytes).TrimEnd('\0')));
        }
    }

    private void ReadFormat(uint format, Action<byte[]> add)
    {
        if (!WindowsClipboardAccess.IsClipboardFormatAvailable(format))
        {
            return;
        }

        try
        {
            byte[] content = WindowsStorageMediumReader.ReadGlobalMemory(
                WindowsClipboardAccess.GetClipboardData(format),
                ClipboardImageLimits.MaximumInputBytes,
                "The clipboard content exceeds the input size limit.");
            add(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A Windows clipboard representation could not be captured");
        }
    }

    [SupportedOSPlatform("windows")]
    private void ReadVirtualFiles(ClipboardDataSnapshot snapshot, CancellationToken ct)
    {
        int result = WindowsClipboardAccess.OleGetClipboard(out IDataObject? dataObject);

        if ((result != WindowsClipboardAccess.Succeeded) || (dataObject is null))
        {
            return;
        }

        try
        {
            WindowsFileDescriptorEncoding encoding = WindowsFileDescriptorEncoding.Unicode;
            bool found = WindowsStorageMediumReader.TryReadGlobalMemory(
                dataObject, unchecked((short)WindowsClipboardAccess.RegisterFormat("FileGroupDescriptorW")),
                ClipboardImageLimits.MaximumDescriptorBytes, "The file descriptor exceeds the input limit.", out byte[] bytes);

            if (!found)
            {
                encoding = WindowsFileDescriptorEncoding.Ansi;
                found = WindowsStorageMediumReader.TryReadGlobalMemory(
                    dataObject, unchecked((short)WindowsClipboardAccess.RegisterFormat("FileGroupDescriptor")),
                    ClipboardImageLimits.MaximumDescriptorBytes, "The file descriptor exceeds the input limit.", out bytes);
            }

            if (!found)
            {
                return;
            }

            IReadOnlyList<WindowsVirtualFileDescriptor> descriptors = WindowsVirtualFileDescriptorParser.Parse(
                bytes, encoding, ClipboardImageLimits.MaximumCandidates);
            short contentFormat = unchecked((short)WindowsClipboardAccess.RegisterFormat("FileContents"));

            for (int index = 0; index < descriptors.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                WindowsVirtualFileDescriptor descriptor = descriptors[index];

                if (descriptor.IsDirectory || (descriptor.DeclaredSize > ClipboardImageLimits.MaximumInputBytes))
                {
                    continue;
                }

                try
                {
                    byte[] content = WindowsStorageMediumReader.ReadIndexedContent(
                        dataObject, contentFormat, index, ClipboardImageLimits.MaximumInputBytes,
                        ImageStreamBuffer.CopyBufferSize, "The virtual image exceeds the input limit.");
                    snapshot.Files.Add(ClipboardImageInput.FromBytes(descriptor.FileName, content));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "A virtual clipboard file could not be captured");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Virtual clipboard descriptors could not be captured");
        }
        finally
        {
            if (Marshal.IsComObject(dataObject))
            {
                Marshal.ReleaseComObject(dataObject);
            }
        }
    }
}

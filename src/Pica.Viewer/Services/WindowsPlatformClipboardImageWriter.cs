using System.Buffers.Binary;

using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class WindowsPlatformClipboardImageWriter : IPlatformClipboardImageWriter
{
    private const uint CopyDropEffect = 1;
    private const string PreferredDropEffectClipboardFormat = "Preferred DropEffect";

    private readonly AvaloniaClipboardDataWriter _clipboardDataWriter;
    private readonly ClipboardImagePreparer _imagePreparer;
    private readonly ViewerWindowPlatformContext _platformContext;

    public WindowsPlatformClipboardImageWriter(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        ClipboardImagePreparer imagePreparer,
        ViewerWindowPlatformContext platformContext)
    {
        _clipboardDataWriter = clipboardDataWriter
            ?? throw new ArgumentNullException(nameof(clipboardDataWriter));
        _imagePreparer = imagePreparer
            ?? throw new ArgumentNullException(nameof(imagePreparer));
        _platformContext = platformContext ?? throw new ArgumentNullException(nameof(platformContext));
    }

    public async Task SetImageAsync(PreparedClipboardImage image, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        byte[] dibContent = WindowsDibV5Builder.Build(image);

        await WriteClipboardAsync(
            () =>
            {
                WindowsClipboardAccess.SetBytes(WindowsClipboardAccess.DibV5Format, dibContent);
                WindowsClipboardAccess.SetBytes(
                    WindowsClipboardAccess.RegisterFormat(PicaClipboardFormats.WindowsPng),
                    image.PngContent);
                WindowsClipboardAccess.SetBytes(
                    WindowsClipboardAccess.RegisterFormat(PicaClipboardFormats.PngMime),
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

        PreparedBitmapPixels preparedBitmap = await _imagePreparer
            .PrepareBitmapAsync(bitmap, ct)
            .ConfigureAwait(false);
        byte[] dibContent = WindowsDibV5Builder.Build(preparedBitmap);
        byte[] fileDropContent = WindowsDropFilesBuilder.Build(filePath);
        byte[] preferredDropEffect = CreatePreferredDropEffect();

        await WriteClipboardAsync(
            () =>
            {
                WindowsClipboardAccess.SetBytes(WindowsClipboardAccess.DibV5Format, dibContent);
                WindowsClipboardAccess.SetBytes(WindowsClipboardAccess.FileDropFormat, fileDropContent);
                WindowsClipboardAccess.SetBytes(
                    WindowsClipboardAccess.RegisterFormat(PreferredDropEffectClipboardFormat),
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
        nint owner = await _platformContext.GetWindowHandleAsync(ct).ConfigureAwait(false);

        await WindowsClipboardAccess.UseAsync(
            owner,
            () =>
            {
                WindowsClipboardAccess.Clear();
                writeContent();
            }, ct).ConfigureAwait(false);
    }

    private static byte[] CreatePreferredDropEffect()
    {
        byte[] content = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(content, CopyDropEffect);

        return content;
    }
}

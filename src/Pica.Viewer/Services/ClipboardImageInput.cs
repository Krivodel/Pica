using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

public sealed class ClipboardImageInput : IDisposable
{
    public string? FileName { get; }
    public string? FilePath { get; }
    public ClipboardImageInputKind Kind { get; }

    public Bitmap? Bitmap => _bitmap;

    private readonly Func<CancellationToken, Task<byte[]>>? _read;
    private Bitmap? _bitmap;

    private ClipboardImageInput(
        string? fileName,
        string? filePath,
        Func<CancellationToken, Task<byte[]>>? read,
        Bitmap? bitmap)
    {
        FileName = fileName;
        FilePath = filePath;
        _read = read;
        _bitmap = bitmap;
        Kind = filePath is not null ? ClipboardImageInputKind.File
            : bitmap is not null ? ClipboardImageInputKind.Bitmap : ClipboardImageInputKind.EncodedData;
    }

    public static ClipboardImageInput FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);

        return new ClipboardImageInput(
            Path.GetFileName(fullPath), fullPath, null, null);
    }

    public static ClipboardImageInput FromBytes(string fileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        byte[] snapshot = (byte[])content.Clone();

        return new ClipboardImageInput(
            Path.GetFileName(fileName), null,
            ct =>
            {
                ct.ThrowIfCancellationRequested();

                return Task.FromResult(snapshot);
            }, null);
    }

    public static ClipboardImageInput FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return new ClipboardImageInput("clipboard.png", null, null, bitmap);
    }

    public async Task<byte[]> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (FilePath is { } filePath)
        {
            await using FileStream stream = File.OpenRead(filePath);

            return await ImageStreamBuffer.ReadAllBytesAsync(
                stream, ClipboardImageLimits.MaximumInputBytes, ct)
                .ConfigureAwait(false);
        }

        if (_read is null)
        {
            throw new InvalidOperationException("A bitmap input has no encoded content.");
        }

        return await _read(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _bitmap, null)?.Dispose();
    }

    internal static ClipboardImageInput FromReader(Func<CancellationToken, Task<byte[]>> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        return new ClipboardImageInput(null, null, read, null);
    }

    internal IPicaImageBitmapLease TakeBitmap()
    {
        Bitmap bitmap = Interlocked.Exchange(ref _bitmap, null)
            ?? throw new InvalidOperationException("The clipboard bitmap has already been transferred.");

        return new ImagePresentationBitmapLease(bitmap, static image => image.Dispose());
    }
}

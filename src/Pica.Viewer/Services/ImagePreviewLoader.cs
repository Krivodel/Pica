using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePreviewLoader
{
    internal const int PreviewDecodeWidth = 128;

    private readonly IImageDecoderResolver _decoderResolver;
    private readonly ILogger<ImagePreviewLoader> _logger;

    public ImagePreviewLoader(
        IImageDecoderResolver decoderResolver,
        ILogger<ImagePreviewLoader> logger)
    {
        _decoderResolver = decoderResolver ?? throw new ArgumentNullException(nameof(decoderResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DecodedImagePreview> LoadAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        return await Task.Run(() => Load(item, ct), ct).ConfigureAwait(false);
    }

    private DecodedImagePreview DecodePreviewFile(
        string previewPath,
        PixelSize sourcePixelSize,
        CancellationToken ct)
    {
        IImageDecoder decoder = _decoderResolver.Resolve(previewPath);
        using FileStream previewStream = File.OpenRead(previewPath);
        Bitmap bitmap = decoder.Decode(previewStream, ct);

        return new DecodedImagePreview(bitmap, sourcePixelSize);
    }

    private static DecodedImagePreview DecodeSourcePreview(
        IImageDecoder decoder,
        Stream sourceStream,
        PixelSize sourcePixelSize,
        CancellationToken ct)
    {
        Bitmap bitmap = decoder.DecodeToWidth(sourceStream, PreviewDecodeWidth, ct);

        return new DecodedImagePreview(bitmap, sourcePixelSize);
    }

    private static string? GetExistingPreviewPath(PicaImageItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PreviewFilePath))
        {
            return null;
        }

        string previewPath = Path.GetFullPath(item.PreviewFilePath);

        return File.Exists(previewPath) ? previewPath : null;
    }

    private DecodedImagePreview Load(PicaImageItem item, CancellationToken ct)
    {
        string sourcePath = Path.GetFullPath(item.FilePath);
        IImageDecoder decoder = _decoderResolver.Resolve(sourcePath);
        using FileStream sourceStream = File.OpenRead(sourcePath);
        PixelSize sourcePixelSize = decoder.ReadPixelSize(sourceStream, ct);
        sourceStream.Position = 0;
        string? existingPreviewPath = GetExistingPreviewPath(item);

        if (existingPreviewPath is not null)
        {
            try
            {
                return DecodePreviewFile(existingPreviewPath, sourcePixelSize, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to use the prebuilt image thumbnail.");
            }
        }

        return DecodeSourcePreview(decoder, sourceStream, sourcePixelSize, ct);
    }
}

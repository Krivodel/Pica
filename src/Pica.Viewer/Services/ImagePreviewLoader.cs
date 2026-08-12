using Avalonia;
using Avalonia.Media.Imaging;
using ImageMagick;
using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePreviewLoader : IImagePreviewLoader
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
        IImageDecoder decoder = _decoderResolver
            .Resolve(previewPath)
            .Decoder;
        byte[] previewData = File.ReadAllBytes(previewPath);
        ct.ThrowIfCancellationRequested();
        using MemoryStream previewStream = new(
            previewData,
            writable: false);
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
        IImageDecoder decoder = _decoderResolver
            .Resolve(sourcePath)
            .Decoder;
        byte[] sourceData = File.ReadAllBytes(sourcePath);
        ct.ThrowIfCancellationRequested();

        if (IsoBmffMixedContentSupport.IsSupportedFile(sourcePath))
        {
            DecodedImagePreview? projectedPreview =
                TryDecodeInitialStillImagePreview(
                    sourceData,
                    decoder,
                    item,
                    ct);

            if (projectedPreview is not null)
            {
                return projectedPreview;
            }
        }

        using MemoryStream sourceStream = new(
            sourceData,
            writable: false);
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

    private DecodedImagePreview? TryDecodeInitialStillImagePreview(
        byte[] sourceData,
        IImageDecoder decoder,
        PicaImageItem item,
        CancellationToken ct)
    {
        IsoBmffTopLevelContent? content =
            IsoBmffTopLevelContentReader.Read(sourceData);

        if ((content is null)
            || !content.StartsWithStillImages)
        {
            return null;
        }

        try
        {
            using IsoBmffMovieBoxProjection projection = new(sourceData);
            using MemoryStream stream = new(
                projection.Data,
                writable: false);
            PixelSize sourcePixelSize = decoder.ReadPixelSize(
                stream,
                ct);
            string? existingPreviewPath =
                GetExistingPreviewPath(item);

            if (existingPreviewPath is not null)
            {
                try
                {
                    return DecodePreviewFile(
                        existingPreviewPath,
                        sourcePixelSize,
                        ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to use the prebuilt image thumbnail.");
                }
            }

            stream.Position = 0;

            return DecodeSourcePreview(
                decoder,
                stream,
                sourcePixelSize,
                ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (MagickException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to decode the initial still-image section preview.");

            return null;
        }
    }
}

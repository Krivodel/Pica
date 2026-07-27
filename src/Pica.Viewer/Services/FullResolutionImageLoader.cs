using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class FullResolutionImageLoader
{
    private static readonly SemaphoreSlim DecodeLock = new(1, 1);

    private readonly IImageDecoderResolver _decoderResolver;

    public FullResolutionImageLoader(IImageDecoderResolver decoderResolver)
    {
        _decoderResolver = decoderResolver ?? throw new ArgumentNullException(nameof(decoderResolver));
    }

    public async Task<Bitmap> LoadAsync(string fullPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        await DecodeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            IImageDecoder decoder = _decoderResolver.Resolve(fullPath);

            return await Task
                .Run(
                    () =>
                    {
                        using FileStream stream = File.OpenRead(fullPath);

                        return decoder.Decode(stream, ct);
                    },
                    ct)
                .ConfigureAwait(false);
        }
        finally
        {
            DecodeLock.Release();
        }
    }
}

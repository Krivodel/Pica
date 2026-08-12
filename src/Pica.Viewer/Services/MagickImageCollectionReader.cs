using ImageMagick;

namespace Pica.Viewer.Services;

internal static class MagickImageCollectionReader
{
    internal static void Ping(
        MagickImageCollection images,
        Stream sourceStream,
        MagickReadSettings? readSettings)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(sourceStream);

        if (readSettings is null)
        {
            images.Ping(sourceStream);
            return;
        }

        images.Ping(sourceStream, readSettings);
    }

    internal static void Read(
        MagickImageCollection images,
        Stream sourceStream,
        MagickReadSettings? readSettings)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(sourceStream);

        if (readSettings is null)
        {
            images.Read(sourceStream);
            return;
        }

        images.Read(sourceStream, readSettings);
    }
}

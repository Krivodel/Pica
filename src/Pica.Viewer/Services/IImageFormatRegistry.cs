using ImageMagick;

namespace Pica.Viewer.Services;

public interface IImageFormatRegistry
{
    IReadOnlyList<string> GetWritableExtensions();

    MagickFormat? GetMultiFrameReadFormat(string fileName);

    bool IsSupportedFileName(string fileName);

    string GetContentType(string fileName);

    string GetExtension(string fileName);
}

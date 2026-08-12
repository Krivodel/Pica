namespace Pica.Viewer.Services;

internal static class IsoBmffMixedContentSupport
{
    internal static bool IsSupportedFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string extension = Path.GetExtension(filePath);

        return extension.Equals(
                PicaImageFormats.AvifExtension,
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                PicaImageFormats.HeicExtension,
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                PicaImageFormats.HeifExtension,
                StringComparison.OrdinalIgnoreCase);
    }
}

namespace Pica.Viewer.Services;

internal static class ImageChannelFileName
{
    internal static string Create(
        ImageChannel channel,
        string sourceFileName)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        string nameWithoutExtension =
            Path.GetFileNameWithoutExtension(sourceFileName);

        return $"{nameWithoutExtension}-{channel.Code}{PicaImageFormats.PngExtension}";
    }
}

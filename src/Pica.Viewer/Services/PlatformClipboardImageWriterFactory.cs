namespace Pica.Viewer.Services;

internal static class PlatformClipboardImageWriterFactory
{
    public static IPlatformClipboardImageWriter Create(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        ClipboardImagePreparer imagePreparer)
    {
        ArgumentNullException.ThrowIfNull(clipboardDataWriter);
        ArgumentNullException.ThrowIfNull(imagePreparer);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatformClipboardImageWriter(
                clipboardDataWriter,
                imagePreparer);
        }

        string pngFormat = OperatingSystem.IsMacOS()
            ? PicaClipboardFormats.MacOsPng
            : PicaClipboardFormats.PngMime;

        return new PngPlatformClipboardImageWriter(clipboardDataWriter, pngFormat);
    }
}

namespace Pica.Viewer.Services;

internal static class PlatformClipboardImageWriterFactory
{
    public static IPlatformClipboardImageWriter Create(
        AvaloniaClipboardDataWriter clipboardDataWriter,
        ClipboardImagePreparer imagePreparer,
        ViewerWindowPlatformContext platformContext)
    {
        ArgumentNullException.ThrowIfNull(clipboardDataWriter);
        ArgumentNullException.ThrowIfNull(imagePreparer);
        ArgumentNullException.ThrowIfNull(platformContext);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatformClipboardImageWriter(
                clipboardDataWriter,
                imagePreparer,
                platformContext);
        }

        string pngFormat = OperatingSystem.IsMacOS()
            ? PicaClipboardFormats.MacOsPng
            : PicaClipboardFormats.PngMime;

        return new PngPlatformClipboardImageWriter(clipboardDataWriter, pngFormat);
    }
}

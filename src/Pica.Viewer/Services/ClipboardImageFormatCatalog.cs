using Avalonia.Input;
using ImageMagick;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImageFormatCatalog
{
    internal IReadOnlyDictionary<string, string> EncodedFormats { get; }

    private readonly IImageFormatRegistry _registry;

    public ClipboardImageFormatCatalog(IImageFormatRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Dictionary<string, string> formats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string extension in registry.GetSupportedExtensions())
        {
            string contentType = registry.GetContentType($"image{extension}");
            formats.TryAdd(contentType, extension);
            formats.TryAdd($"public.{extension.TrimStart('.')}", extension);
        }

        formats[PicaClipboardFormats.WindowsPng] = PicaImageFormats.PngExtension;
        formats[PicaClipboardFormats.MacOsPng] = PicaImageFormats.PngExtension;
        formats["JFIF"] = ".jpeg";
        formats["image/jpg"] = ".jpeg";
        formats["org.webmproject.webp"] = ".webp";
        formats["com.microsoft.bmp"] = ".bmp";
        formats["com.microsoft.ico"] = ".ico";
        formats["image/vnd.microsoft.icon"] = ".ico";
        EncodedFormats = formats;
    }

    internal bool IsImageFormat(DataFormat format)
    {
        return EncodedFormats.ContainsKey(format.Identifier)
            || format.Identifier.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    internal string GetFileName(string identifier)
    {
        return $"clipboard{EncodedFormats.GetValueOrDefault(identifier, ".img")}";
    }

    internal string DetectFileName(byte[] bytes)
    {
        MagickImageInfo info = new(bytes);
        string extension = info.Format switch
        {
            MagickFormat.Bmp2 or MagickFormat.Bmp3 or MagickFormat.Dib => ".bmp",
            MagickFormat.Jpg or MagickFormat.Jpeg => ".jpeg",
            MagickFormat.Tif or MagickFormat.Tiff => PicaImageFormats.TiffExtension,
            _ => $".{info.Format.ToString().ToLowerInvariant()}"
        };

        if (!_registry.IsSupportedFileName($"image{extension}"))
        {
            throw new NotSupportedException("The clipboard image format is not supported by Pica.");
        }

        return $"clipboard{extension}";
    }
}

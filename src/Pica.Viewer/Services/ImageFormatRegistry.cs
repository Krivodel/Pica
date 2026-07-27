namespace Pica.Viewer.Services;

public sealed class ImageFormatRegistry : IImageFormatRegistry, IImageDecoderResolver
{
    private static readonly IImageDecoder DefaultDecoder = new AvaloniaBitmapDecoder();
    private static readonly IReadOnlyDictionary<string, ImageFormatDefinition> FormatsByExtension =
        new Dictionary<string, ImageFormatDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [PicaImageFormats.PngExtension] = new(
                PicaImageFormats.PngContentType,
                DefaultDecoder),
            [".jpg"] = new(PicaImageFormats.JpegContentType, DefaultDecoder),
            [".jpeg"] = new(PicaImageFormats.JpegContentType, DefaultDecoder),
            [".webp"] = new("image/webp", DefaultDecoder),
            [".bmp"] = new("image/bmp", DefaultDecoder),
            [".gif"] = new("image/gif", DefaultDecoder),
            [".ico"] = new("image/x-icon", DefaultDecoder),
            [PicaImageFormats.HeicExtension] = new(
                PicaImageFormats.HeicContentType,
                new MagickHeicImageDecoder())
        };

    public bool IsSupportedFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return FormatsByExtension.ContainsKey(extension);
    }

    public string GetContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        ImageFormatDefinition? format = FormatsByExtension.GetValueOrDefault(extension);

        return format?.ContentType ?? PicaImageFormats.PngContentType;
    }

    public string GetExtension(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return string.IsNullOrWhiteSpace(extension) ? PicaImageFormats.PngExtension : extension;
    }

    IImageDecoder IImageDecoderResolver.Resolve(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        ImageFormatDefinition? format = FormatsByExtension.GetValueOrDefault(extension);

        return format?.Decoder ?? DefaultDecoder;
    }

    private sealed record ImageFormatDefinition(
        string ContentType,
        IImageDecoder Decoder);
}

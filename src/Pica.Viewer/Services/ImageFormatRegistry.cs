namespace Pica.Viewer.Services;

public sealed class ImageFormatRegistry : IImageFormatRegistry, IImageDecoderResolver
{
    private static readonly IImageDecoder DefaultDecoder = new AvaloniaBitmapDecoder();
    private static readonly IImageDecoder MagickDecoder = new MagickImageDecoder();
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
            [PicaImageFormats.AvifExtension] = new(
                PicaImageFormats.AvifContentType,
                MagickDecoder),
            [PicaImageFormats.HeicExtension] = new(
                PicaImageFormats.HeicContentType,
                MagickDecoder),
            [PicaImageFormats.HeifExtension] = new(
                PicaImageFormats.HeifContentType,
                MagickDecoder),
            [PicaImageFormats.TifExtension] = new(
                PicaImageFormats.TiffContentType,
                MagickDecoder),
            [PicaImageFormats.TiffExtension] = new(
                PicaImageFormats.TiffContentType,
                MagickDecoder)
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

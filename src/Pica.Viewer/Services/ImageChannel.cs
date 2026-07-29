namespace Pica.Viewer.Services;

internal sealed record ImageChannel
{
    internal static ImageChannel Red { get; } = new ImageChannel("R", 2);
    internal static ImageChannel Green { get; } = new ImageChannel("G", 1);
    internal static ImageChannel Blue { get; } = new ImageChannel("B", 0);
    internal static ImageChannel Alpha { get; } = new ImageChannel("A", 3);
    internal static IReadOnlyList<ImageChannel> ColorChannels { get; } =
        Array.AsReadOnly(new ImageChannel[] { Red, Green, Blue });
    internal static IReadOnlyList<ImageChannel> ColorAndAlphaChannels { get; } =
        Array.AsReadOnly(new ImageChannel[] { Red, Green, Blue, Alpha });

    internal string Code { get; }
    internal int BgraOffset { get; }

    private ImageChannel(
        string code,
        int bgraOffset)
    {
        Code = code;
        BgraOffset = bgraOffset;
    }

    internal string CreateFileName(string sourceFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);

        return $"{nameWithoutExtension}-{Code}{PicaImageFormats.PngExtension}";
    }
}

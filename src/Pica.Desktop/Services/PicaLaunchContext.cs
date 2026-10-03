namespace Pica.Desktop.Services;

internal sealed record PicaLaunchContext(long? SourceWindowHandle)
{
    internal static PicaLaunchContext Empty { get; } =
        new PicaLaunchContext((long?)null);
}

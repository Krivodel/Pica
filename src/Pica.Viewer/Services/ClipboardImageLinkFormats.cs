using System.Text;

namespace Pica.Viewer.Services;

internal static class ClipboardImageLinkFormats
{
    internal static IReadOnlyDictionary<string, Encoding> Formats { get; } =
        new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase)
        {
            ["DownloadURL"] = Encoding.UTF8,
            ["public.url"] = Encoding.UTF8,
            ["text/uri-list"] = Encoding.UTF8,
            ["text/x-moz-url"] = Encoding.Unicode,
            ["UniformResourceLocator"] = Encoding.UTF8,
            ["UniformResourceLocatorW"] = Encoding.Unicode,
            ["HTML Format"] = Encoding.UTF8,
            ["public.html"] = Encoding.UTF8,
            ["text/html"] = Encoding.UTF8
        };
}

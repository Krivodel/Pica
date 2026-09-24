using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

public sealed class OpenWithApplicationIconStore : IDisposable
{
    private readonly List<Bitmap> _icons = [];

    public Image? CreateIcon(OpenWithApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.IconPngContent is not { Length: > 0 } pngContent)
        {
            return null;
        }

        using MemoryStream stream = new(pngContent, writable: false);
        Bitmap bitmap = new(stream);
        _icons.Add(bitmap);

        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform
        };
    }

    public void Clear()
    {
        foreach (Bitmap icon in _icons)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    public void Dispose()
    {
        Clear();
    }
}

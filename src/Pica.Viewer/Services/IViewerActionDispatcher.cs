using Avalonia.Media.Imaging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

public interface IViewerActionDispatcher
{
    string GetCurrentImageActionDisplayName(
        PicaActionDefinition action,
        PicaImageItem item)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        return action.DisplayName;
    }

    bool CanDispatchBitmapWithoutEncoding(
        PicaActionDefinition action,
        PicaImageItem item)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        return false;
    }

    Task DispatchBitmapAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        Bitmap bitmap,
        string fileName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ct.ThrowIfCancellationRequested();

        throw new NotSupportedException(
            "The viewer action dispatcher does not support direct bitmap dispatch.");
    }

    Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct);

    Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct);

    Task DispatchDerivedImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        string fileName,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(pngContent);

        return DispatchSelectionAsync(
            action,
            item,
            pngContent,
            ct);
    }
}

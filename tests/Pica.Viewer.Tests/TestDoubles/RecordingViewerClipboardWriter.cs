using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerClipboardWriter : IViewerClipboardWriter
{
    internal int PreparedImageCount { get; private set; }
    internal int FileCount { get; private set; }
    internal int FileWithImageCount { get; private set; }
    internal PreparedClipboardImage? LastPreparedImage { get; private set; }

    public Task FlushAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task SetImageAsync(
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task SetPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ct.ThrowIfCancellationRequested();
        PreparedImageCount++;
        LastPreparedImage = image;

        return Task.CompletedTask;
    }

    public Task SetFileAsync(
        IStorageFile file,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ct.ThrowIfCancellationRequested();
        FileCount++;

        return Task.CompletedTask;
    }

    public Task SetFileWithImageAsync(
        IStorageFile file,
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bitmap);
        ct.ThrowIfCancellationRequested();
        FileWithImageCount++;

        return Task.CompletedTask;
    }
}

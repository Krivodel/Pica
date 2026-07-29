using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerImageCommandService :
    IViewerImageCommandService
{
    public string? PreparedOpenWithFilePath { get; private set; }

    public event EventHandler? PreparedSelectionSaved;

    internal int CopyCurrentCount { get; private set; }
    internal int CopySelectionCount { get; private set; }
    internal int DispatchCurrentCount { get; private set; }
    internal int DispatchSelectionCount { get; private set; }
    internal int SaveCurrentCount { get; private set; }
    internal int SaveSelectionCount { get; private set; }
    internal int PrepareCurrentOpenWithCount { get; private set; }
    internal int PrepareSelectionOpenWithCount { get; private set; }
    internal PicaActionDefinition? LastAction { get; private set; }
    internal PicaImageItem? LastItem { get; private set; }
    internal PreparedClipboardImage? LastImage { get; private set; }
    internal Exception? PrepareSelectionException { get; set; }
    internal bool CompleteSelectionSave { get; set; } = true;
    internal bool BlockCopyCurrent { get; set; }
    internal Task CopyCurrentStarted => _copyCurrentStarted.Task;

    private readonly TaskCompletionSource _copyCurrentStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _copyCurrentCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task CopyCurrentAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CopyCurrentCount++;

        if (!BlockCopyCurrent)
        {
            return Task.CompletedTask;
        }

        _copyCurrentStarted.TrySetResult();

        return _copyCurrentCompletion.Task.WaitAsync(ct);
    }

    public Task CopyPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        LastImage = image
            ?? throw new ArgumentNullException(nameof(image));
        ct.ThrowIfCancellationRequested();
        CopySelectionCount++;

        return Task.CompletedTask;
    }

    public Task<PreparedClipboardImage?> PrepareSelectionAsync(
        ImagePixelSelection selection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ct.ThrowIfCancellationRequested();

        if (PrepareSelectionException is { } exception)
        {
            throw exception;
        }

        PreparedClipboardImage? image = LastImage;

        return Task.FromResult(image);
    }

    public Task DispatchCurrentAsync(
        PicaActionDefinition action,
        CancellationToken ct)
    {
        LastAction = action
            ?? throw new ArgumentNullException(nameof(action));
        ct.ThrowIfCancellationRequested();
        DispatchCurrentCount++;

        return Task.CompletedTask;
    }

    public Task DispatchPreparedSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        LastAction = action
            ?? throw new ArgumentNullException(nameof(action));
        LastItem = item ?? throw new ArgumentNullException(nameof(item));
        LastImage = image
            ?? throw new ArgumentNullException(nameof(image));
        ct.ThrowIfCancellationRequested();
        DispatchSelectionCount++;

        return Task.CompletedTask;
    }

    public Task SaveCurrentAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SaveCurrentCount++;

        return Task.CompletedTask;
    }

    public Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        LastImage = image
            ?? throw new ArgumentNullException(nameof(image));
        ct.ThrowIfCancellationRequested();
        SaveSelectionCount++;

        if (CompleteSelectionSave)
        {
            PreparedSelectionSaved?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public string GetCurrentOpenWithAssociationFilePath()
    {
        return "C:\\Images\\image.png";
    }

    public Task PrepareCurrentOpenWithFileAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PrepareCurrentOpenWithCount++;
        PreparedOpenWithFilePath = "C:\\Images\\image.png";

        return Task.CompletedTask;
    }

    public Task PrepareSelectionOpenWithFileAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        LastImage = image
            ?? throw new ArgumentNullException(nameof(image));
        ct.ThrowIfCancellationRequested();
        PrepareSelectionOpenWithCount++;
        PreparedOpenWithFilePath = "C:\\Temp\\selection.png";

        return Task.CompletedTask;
    }

    internal void CompleteCopyCurrent()
    {
        _copyCurrentCompletion.TrySetResult();
    }
}

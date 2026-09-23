using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerImageCommandService :
    IViewerImageCommandService
{
    public string? PreparedOpenWithFilePath { get; private set; }

    public bool CanOpenCurrentImageWithApplication { get; set; } = true;

    public event EventHandler? PreparedSelectionSaved;
    public event EventHandler? SaveWritingStarted;

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
    internal bool BlockSaveCurrent { get; set; }
    internal bool BlockSaveSelection { get; set; }
    internal Task CopyCurrentStarted => _copyCurrentStarted.Task;
    internal Task SaveCurrentStarted => _saveCurrentStarted.Task;
    internal Task SaveSelectionStarted => _saveSelectionStarted.Task;

    private readonly TaskCompletionSource _copyCurrentStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _copyCurrentCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _saveCurrentStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _saveCurrentCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _saveSelectionStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _saveSelectionCompletion =
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
        SaveWritingStarted?.Invoke(this, EventArgs.Empty);

        if (!BlockSaveCurrent)
        {
            return Task.CompletedTask;
        }

        _saveCurrentStarted.TrySetResult();

        return _saveCurrentCompletion.Task.WaitAsync(ct);
    }

    public async Task SavePreparedSelectionAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        LastImage = image
            ?? throw new ArgumentNullException(nameof(image));
        ct.ThrowIfCancellationRequested();
        SaveSelectionCount++;
        SaveWritingStarted?.Invoke(this, EventArgs.Empty);

        if (BlockSaveSelection)
        {
            _saveSelectionStarted.TrySetResult();
            await _saveSelectionCompletion.Task.WaitAsync(ct);
        }

        if (CompleteSelectionSave)
        {
            PreparedSelectionSaved?.Invoke(this, EventArgs.Empty);
        }
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

    internal void CompleteSaveCurrent()
    {
        _saveCurrentCompletion.TrySetResult();
    }

    internal void FailSaveCurrent(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _saveCurrentCompletion.TrySetException(exception);
    }

    internal void CompleteSaveSelection()
    {
        _saveSelectionCompletion.TrySetResult();
    }
}

using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class AvaloniaViewerFilePickerService :
    IViewerFilePickerService
{
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly ViewerWindowPlatformContext _platformContext;

    internal AvaloniaViewerFilePickerService(
        IViewerUiDispatcher uiDispatcher,
        ViewerWindowPlatformContext platformContext)
    {
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _platformContext = platformContext
            ?? throw new ArgumentNullException(nameof(platformContext));
    }

    public async Task<IStorageFile?> GetFileFromPathAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        IStorageProvider? storageProvider = await _platformContext
            .GetStorageProviderAsync(ct)
            .ConfigureAwait(false);

        if (storageProvider is null)
        {
            return null;
        }

        Task<IStorageFile?> fileTask = await _uiDispatcher
            .InvokeAsync(
                () => storageProvider.TryGetFileFromPathAsync(filePath),
                ct)
            .ConfigureAwait(false);
        IStorageFile? file = await fileTask
            .WaitAsync(ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        return file;
    }

    public async Task<IStorageFile?> SelectSaveDestinationAsync(
        FilePickerSaveOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();
        IStorageProvider? storageProvider = await _platformContext
            .GetStorageProviderAsync(ct)
            .ConfigureAwait(false);

        if (storageProvider is null)
        {
            return null;
        }

        bool canSave = await _uiDispatcher
            .InvokeAsync(
                () => storageProvider.CanSave,
                ct)
            .ConfigureAwait(false);

        if (!canSave)
        {
            return null;
        }

        Task<IStorageFile?> destinationTask = await _uiDispatcher
            .InvokeAsync(
                () => storageProvider.SaveFilePickerAsync(options),
                ct)
            .ConfigureAwait(false);
        IStorageFile? destination = await destinationTask
            .WaitAsync(ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        return destination;
    }
}

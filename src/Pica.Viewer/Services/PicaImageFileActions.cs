using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class PicaImageFileActions : IPicaImageFileActions
{
    public bool SupportsOpenWith => _platformFileActions.SupportsOpenWith;

    private readonly IImageFormatRegistry _formatRegistry;
    private readonly IViewerUiDispatcher _uiDispatcher;
    private readonly IPlatformFileActions _platformFileActions;
    private IStorageProvider? _storageProvider;

    public PicaImageFileActions(
        IImageFormatRegistry formatRegistry,
        IViewerUiDispatcher uiDispatcher,
        IPlatformFileActions platformFileActions)
    {
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
    }

    public void Attach(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider
            ?? throw new ArgumentNullException(nameof(storageProvider));
    }

    public Task<bool> SaveAsAsync(string filePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        IStorageProvider storageProvider = _storageProvider
            ?? throw new InvalidOperationException(
                "A storage provider must be attached before saving an image.");
        ViewerWindowPlatformContext platformContext = new(storageProvider, null);
        AvaloniaViewerFilePickerService picker = new(_uiDispatcher, platformContext);
        ViewerImageSaveService saveService = new(picker, _formatRegistry);

        return saveService.SaveFileAsync(
            filePath,
            Path.GetFileName(filePath),
            ct);
    }

    public Task<IReadOnlyList<OpenWithApplication>> GetOpenWithApplicationsAsync(
        string filePath,
        CancellationToken ct)
    {
        return _platformFileActions.GetOpenWithApplicationsAsync(filePath, ct);
    }

    public Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        return _platformFileActions.OpenWithAsync(filePath, application, ct);
    }

    public Task ChooseApplicationAsync(string filePath, CancellationToken ct)
    {
        return _platformFileActions.ChooseApplicationAsync(filePath, ct);
    }
}

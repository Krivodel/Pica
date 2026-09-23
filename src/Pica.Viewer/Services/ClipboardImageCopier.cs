using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImageCopier : IClipboardImageCopier
{
    private readonly IImageDecoderResolver _decoderResolver;
    private readonly ViewerClipboardFactory _clipboardFactory;
    private readonly IViewerUiDispatcher _uiDispatcher;

    public ClipboardImageCopier(
        IImageDecoderResolver decoderResolver,
        ViewerClipboardFactory clipboardFactory,
        IViewerUiDispatcher uiDispatcher)
    {
        _decoderResolver = decoderResolver
            ?? throw new ArgumentNullException(nameof(decoderResolver));
        _clipboardFactory = clipboardFactory
            ?? throw new ArgumentNullException(nameof(clipboardFactory));
        _uiDispatcher = uiDispatcher
            ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public async Task CopyFileAsync(
        string imagePath,
        IClipboard clipboard,
        IStorageProvider storageProvider,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(storageProvider);
        ct.ThrowIfCancellationRequested();

        using Bitmap bitmap = await LoadFileAsync(imagePath, ct)
            .ConfigureAwait(false);

        ViewerWindowPlatformContext platformContext = new(storageProvider, clipboard);
        using ViewerClipboardServices clipboardServices = _clipboardFactory.Create(
            platformContext);
        IViewerFilePickerService filePickerService = new AvaloniaViewerFilePickerService(
            _uiDispatcher,
            platformContext);
        ViewerClipboardFileCopy fileCopy = new(
            filePickerService,
            clipboardServices.Writer);
        await fileCopy
            .CopyAsync(imagePath, bitmap, ct)
            .ConfigureAwait(false);
        await clipboardServices
            .FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task<Bitmap> LoadFileAsync(
        string imagePath,
        CancellationToken ct)
    {
        ImageDecoderSelection decoderSelection = _decoderResolver.Resolve(imagePath);
        return await Task.Run(
            () => DecodeImage(imagePath, decoderSelection, ct),
            ct).ConfigureAwait(false);
    }

    private static Bitmap DecodeImage(
        string imagePath,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using FileStream source = File.OpenRead(imagePath);

        return decoderSelection.Decoder.Decode(source, ct);
    }
}

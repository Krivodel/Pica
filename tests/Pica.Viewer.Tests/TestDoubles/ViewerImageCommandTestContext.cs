using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ViewerImageCommandTestContext : IDisposable
{
    internal PicaImageItem Item { get; }
    internal ImageViewerSessionViewModel Session { get; }
    internal RecordingViewerClipboardWriter ClipboardWriter { get; }
    internal RecordingViewerActionDispatcher ActionDispatcher { get; }
    internal ReadyImagePresentationReadiness Readiness { get; }
    internal ViewerImageCommandService CommandService { get; }

    private readonly ImagePresentationController _presentation;

    private ViewerImageCommandTestContext(
        PicaImageItem item,
        ImageViewerSessionViewModel session,
        RecordingViewerClipboardWriter clipboardWriter,
        RecordingViewerActionDispatcher actionDispatcher,
        ReadyImagePresentationReadiness readiness,
        ViewerImageCommandService commandService,
        ImagePresentationController presentation)
    {
        Item = item;
        Session = session;
        ClipboardWriter = clipboardWriter;
        ActionDispatcher = actionDispatcher;
        Readiness = readiness;
        CommandService = commandService;
        _presentation = presentation;
    }

    public void Dispose()
    {
        CommandService.Dispose();
        _presentation.Dispose();
        Session.Dispose();
    }

    internal static async Task<ViewerImageCommandTestContext> CreateAsync(
        IStorageProvider? storageProvider = null)
    {
        Guid itemId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");
        PicaImageItem item = new(
            itemId,
            "image.png",
            "image.png");
        PicaViewerRequest request = new(
            new PicaImageItem[] { item },
            itemId,
            Array.Empty<PicaActionDefinition>(),
            null);
        ImageViewerSession sessionState = new(request, true);
        ImageViewerSessionViewModel session = new(sessionState);
        RecordingImageChannelBitmapLoader channelLoader = new();
        ImagePresentationController presentation = new(
            sessionState,
            channelLoader,
            new AvaloniaViewerUiDispatcher(),
            NullLogger<ImagePresentationController>.Instance);
        Bitmap sourceBitmap = BgraBitmapTestData.CreateBitmap();
        presentation.ReplaceFullResolutionBitmap(
            item,
            sourceBitmap);
        RecordingViewerClipboardWriter clipboardWriter = new();
        RecordingViewerActionDispatcher actionDispatcher = new();
        ViewerWindowPlatformContext platformContext = new(
            storageProvider,
            null);
        IViewerFilePickerService filePickerService =
            new AvaloniaViewerFilePickerService(
                new AvaloniaViewerUiDispatcher(),
                platformContext);
        ViewerImageOperations imageOperations = new(
            clipboardWriter,
            filePickerService,
            new ImageFormatRegistry(),
            new PngImageEncoder(),
            actionDispatcher);
        TemporaryImageFileStore temporaryFileStore = new(
            NullLogger<TemporaryImageFileStore>.Instance);
        ReadyImagePresentationReadiness readiness = new();
        ViewerImageCommandService commandService = new(
            sessionState,
            presentation,
            readiness,
            imageOperations,
            new ClipboardImagePreparer(),
            temporaryFileStore);

        session.SelectChannelImageModeCommand.Execute(null);
        await presentation.WaitForSelectedChannelAsync(
            CancellationToken.None);

        return new ViewerImageCommandTestContext(
            item,
            session,
            clipboardWriter,
            actionDispatcher,
            readiness,
            commandService,
            presentation);
    }
}

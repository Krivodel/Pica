using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal sealed class ViewerClipboardFactory
{
    private readonly ClipboardImagePreparer _imagePreparer;
    private readonly ClipboardFlushCoordinator _flushCoordinator;
    private readonly ILogger<AvaloniaClipboardDataWriter> _dataWriterLogger;

    public ViewerClipboardFactory(
        ClipboardImagePreparer imagePreparer,
        ClipboardFlushCoordinator flushCoordinator,
        ILogger<AvaloniaClipboardDataWriter> dataWriterLogger)
    {
        _imagePreparer = imagePreparer
            ?? throw new ArgumentNullException(nameof(imagePreparer));
        _flushCoordinator = flushCoordinator
            ?? throw new ArgumentNullException(nameof(flushCoordinator));
        _dataWriterLogger = dataWriterLogger
            ?? throw new ArgumentNullException(nameof(dataWriterLogger));
    }

    internal ViewerClipboardServices Create(
        ViewerWindowPlatformContext platformContext)
    {
        ArgumentNullException.ThrowIfNull(platformContext);
        AvaloniaClipboardDataWriter dataWriter = new(
            platformContext,
            _dataWriterLogger);
        IPlatformClipboardImageWriter platformWriter =
            PlatformClipboardImageWriterFactory.Create(
                dataWriter,
                _imagePreparer);
        ClipboardImageWriter writer = new(
            dataWriter,
            platformWriter);
        IDisposable registration = _flushCoordinator.Register(dataWriter);

        return new ViewerClipboardServices(
            writer,
            dataWriter,
            registration);
    }
}

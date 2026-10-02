namespace Pica.Viewer.Services;

internal sealed class ViewerClipboardServices : IDisposable
{
    internal IViewerClipboardWriter Writer { get; }
    internal IClipboardImageReader Reader { get; }

    private readonly AvaloniaClipboardDataWriter _dataWriter;
    private readonly IDisposable _flushRegistration;

    internal ViewerClipboardServices(
        IViewerClipboardWriter writer,
        AvaloniaClipboardDataWriter dataWriter,
        IDisposable flushRegistration,
        IClipboardImageReader reader)
    {
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _dataWriter = dataWriter
            ?? throw new ArgumentNullException(nameof(dataWriter));
        _flushRegistration = flushRegistration
            ?? throw new ArgumentNullException(nameof(flushRegistration));
    }

    public void Dispose()
    {
        _flushRegistration.Dispose();
        _dataWriter.Dispose();
    }

    internal async Task FlushAsync(CancellationToken ct)
    {
        await Writer
            .FlushAsync(ct)
            .ConfigureAwait(false);
    }
}

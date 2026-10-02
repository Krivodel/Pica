using System.ComponentModel;

using Microsoft.Extensions.Logging;

using Avalonia.Input.Platform;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ViewerClipboardPasteService : IViewerClipboardPasteService
{
    private readonly ImageViewerSession _session;
    private readonly ImageLoadCoordinator _loadCoordinator;
    private readonly ViewerWindowPlatformContext _platformContext;
    private readonly IClipboardImageReader _reader;
    private readonly FullResolutionImageLoader _loader;
    private readonly ClipboardImageFormatCatalog _formats;
    private readonly IViewerUiDispatcher _dispatcher;
    private readonly ILogger<ViewerClipboardPasteService> _logger;
    private OperationCancellation? _preparationCancellation;
    private bool _disposed;

    internal ViewerClipboardPasteService(
        ImageViewerSession session,
        ImageLoadCoordinator loadCoordinator,
        ViewerWindowPlatformContext platformContext,
        IClipboardImageReader reader,
        FullResolutionImageLoader loader,
        ClipboardImageFormatCatalog formats,
        IViewerUiDispatcher dispatcher,
        ILogger<ViewerClipboardPasteService> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _loadCoordinator = loadCoordinator ?? throw new ArgumentNullException(nameof(loadCoordinator));
        _platformContext = platformContext ?? throw new ArgumentNullException(nameof(platformContext));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _session.NavigationRequested += OnNavigationRequested;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public async Task PasteAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelPreparation();
        OperationCancellation cancellation = new(ct);
        _preparationCancellation = cancellation;
        IReadOnlyList<ClipboardImageInput> candidates = Array.Empty<ClipboardImageInput>();

        try
        {
            IClipboard? clipboard = await _platformContext.GetClipboardAsync(cancellation.Token).ConfigureAwait(false);

            if (clipboard is null)
            {
                throw new InvalidOperationException("The window clipboard is unavailable.");
            }

            candidates = await _reader.ReadAsync(clipboard, cancellation.Token).ConfigureAwait(false);

            foreach (ClipboardImageInput candidate in candidates)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                DecodedImageContent? content = null;
                IPicaImageBitmapLease? lease = null;

                try
                {
                    string? fileName = candidate.FileName;

                    if (candidate.Bitmap is not null)
                    {
                        lease = candidate.TakeBitmap();
                    }
                    else
                    {
                        byte[] bytes = await candidate.ReadAsync(cancellation.Token).ConfigureAwait(false);
                        string detectedName = await Task.Run(() => _formats.DetectFileName(bytes), cancellation.Token)
                            .ConfigureAwait(false);
                        content = await _loader.LoadAsync(bytes, detectedName, cancellation.Token).ConfigureAwait(false);
                        fileName ??= detectedName;
                    }

                    ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
                    PicaImageItem item = new(Guid.NewGuid(), candidate.FilePath ?? string.Empty, fileName);
                    DecodedImageContent? preparedContent = content;
                    IPicaImageBitmapLease? preparedLease = lease;
                    await _dispatcher.InvokeAsync(() =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        ObjectDisposedException.ThrowIf(_disposed, this);
                        if (preparedContent is not null)
                        {
                            _loadCoordinator.ApplyClipboardImage(item, preparedContent);
                        }
                        else if (preparedLease is not null)
                        {
                            _loadCoordinator.ApplyClipboardImage(item, preparedLease);
                        }
                        content = null;
                        lease = null;
                    }, cancellation.Token).ConfigureAwait(false);

                    return;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "A clipboard image candidate could not be opened");
                }
                finally
                {
                    content?.Dispose();
                    lease?.Dispose();
                }
            }

            cancellation.Token.ThrowIfCancellationRequested();
            throw new InvalidDataException("The clipboard contains no decodable image.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (cancellation.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "A canceled clipboard preparation completed with an error");
        }
        finally
        {
            foreach (ClipboardImageInput candidate in candidates)
            {
                candidate.Dispose();
            }

            Interlocked.CompareExchange(ref _preparationCancellation, null, cancellation);
            cancellation.Complete();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _session.NavigationRequested -= OnNavigationRequested;
        _session.PropertyChanged -= OnSessionPropertyChanged;
        CancelPreparation();
    }

    private void CancelPreparation()
    {
        Interlocked.Exchange(ref _preparationCancellation, null)?.Cancel();
    }

    private void OnNavigationRequested(object? sender, EventArgs e)
    {
        CancelPreparation();
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewerSession.SelectedIndex))
        {
            CancelPreparation();
        }
    }
}

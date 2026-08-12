namespace Pica.Viewer.Services;

internal sealed class DecodedImageContentGroup
{
    internal ImageContentGroupDefinition Definition { get; }
    internal bool IsLoaded
    {
        get
        {
            lock (_sync)
            {
                return _image is not null;
            }
        }
    }

    private readonly object _sync = new();
    private Func<CancellationToken, Task<DecodedImage>>? _loader;
    private readonly OperationCancellation _loadingCancellation = new();
    private Task<DecodedImage>? _loadingTask;
    private DecodedImage? _image;
    private bool _stopped;
    private bool _imageTaken;

    internal DecodedImageContentGroup(
        ImageContentGroupDefinition definition,
        DecodedImage image)
    {
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _loadingCancellation.Complete();
    }

    internal DecodedImageContentGroup(
        ImageContentGroupDefinition definition,
        Func<CancellationToken, Task<DecodedImage>> loader)
    {
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    internal Task<DecodedImage> LoadAsync(CancellationToken ct)
    {
        Task<DecodedImage> loadingTask;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_stopped, this);

            if (_image is not null)
            {
                return Task.FromResult(_image);
            }

            _loadingTask ??= LoadCoreAsync();
            loadingTask = _loadingTask;
        }

        return loadingTask.WaitAsync(ct);
    }

    internal DecodedImage GetRequiredImage()
    {
        lock (_sync)
        {
            return _image
                ?? throw new InvalidOperationException(
                    "The image content group has not been loaded.");
        }
    }

    internal IReadOnlyList<DecodedImage> StopAndGetLoadedImages()
    {
        DecodedImage? image = null;
        bool shouldCompleteCancellation;

        lock (_sync)
        {
            _stopped = true;
            shouldCompleteCancellation = _loadingTask is null;

            if (shouldCompleteCancellation)
            {
                _loader = null;
            }

            if (!_imageTaken && (_image is not null))
            {
                _imageTaken = true;
                image = _image;
            }
        }

        _loadingCancellation.Cancel();

        if (shouldCompleteCancellation)
        {
            _loadingCancellation.Complete();
        }

        return image is null ? [] : [image];
    }

    internal void CancelLoading()
    {
        bool shouldCompleteCancellation;

        lock (_sync)
        {
            _stopped = true;
            shouldCompleteCancellation = _loadingTask is null;

            if (shouldCompleteCancellation)
            {
                _loader = null;
            }
        }

        _loadingCancellation.Cancel();

        if (shouldCompleteCancellation)
        {
            _loadingCancellation.Complete();
        }
    }

    internal Task? GetLoadingCompletion()
    {
        lock (_sync)
        {
            return _loadingTask;
        }
    }

    private async Task<DecodedImage> LoadCoreAsync()
    {
        Func<CancellationToken, Task<DecodedImage>> loader = _loader
            ?? throw new InvalidOperationException(
                "The image content group does not have a loader.");

        try
        {
            DecodedImage image = await loader(
                _loadingCancellation.Token).ConfigureAwait(false);
            bool isStopped;

            lock (_sync)
            {
                isStopped = _stopped;

                if (!isStopped)
                {
                    _image = image;
                }
            }

            if (isStopped)
            {
                image.Dispose();
                throw new OperationCanceledException(
                    _loadingCancellation.Token);
            }

            return image;
        }
        finally
        {
            lock (_sync)
            {
                _loader = null;
            }

            _loadingCancellation.Complete();
        }
    }
}

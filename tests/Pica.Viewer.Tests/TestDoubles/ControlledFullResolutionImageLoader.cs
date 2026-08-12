using Avalonia.Media.Imaging;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ControlledFullResolutionImageLoader :
    IFullResolutionImageLoader
{
    private readonly object _sync = new();
    private readonly Dictionary<string, TaskCompletionSource<DecodedImageContent>>
        _completions;
    private readonly Dictionary<string, TaskCompletionSource<bool>>
        _starts;
    private readonly Dictionary<string, CancellationToken> _tokens;

    internal ControlledFullResolutionImageLoader(
        IReadOnlyList<string> fullPaths)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);
        _completions =
            new Dictionary<string, TaskCompletionSource<DecodedImageContent>>(
                StringComparer.OrdinalIgnoreCase);
        _starts =
            new Dictionary<string, TaskCompletionSource<bool>>(
                StringComparer.OrdinalIgnoreCase);
        _tokens = new Dictionary<string, CancellationToken>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string fullPath in fullPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
            _completions.Add(
                fullPath,
                new TaskCompletionSource<DecodedImageContent>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
            _starts.Add(
                fullPath,
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }

    public Task<DecodedImageContent> LoadAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        TaskCompletionSource<DecodedImageContent> completion;
        TaskCompletionSource<bool> start;

        lock (_sync)
        {
            completion = _completions[fullPath];
            start = _starts[fullPath];
            _tokens[fullPath] = ct;
        }

        start.TrySetResult(true);

        return completion.Task;
    }

    internal void Complete(string fullPath, Bitmap bitmap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(bitmap);
        _completions[fullPath].TrySetResult(
            DecodedImageContent.CreateSingle(
                DecodedImage.CreateSingle(bitmap)));
    }

    internal void Complete(
        string fullPath,
        DecodedImage image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(image);
        _completions[fullPath].TrySetResult(
            DecodedImageContent.CreateSingle(image));
    }

    internal void Complete(
        string fullPath,
        DecodedImageContent content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(content);
        _completions[fullPath].TrySetResult(content);
    }

    internal CancellationToken GetCancellationToken(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        lock (_sync)
        {
            return _tokens[fullPath];
        }
    }

    internal async Task WaitUntilStartedAsync(
        string fullPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        await _starts[fullPath].Task
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }
}

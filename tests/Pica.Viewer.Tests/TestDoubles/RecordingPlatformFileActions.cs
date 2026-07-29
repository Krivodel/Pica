using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingPlatformFileActions :
    IPlatformFileActions
{
    public bool SupportsOpenWith { get; set; } = true;

    internal IReadOnlyList<OpenWithApplication> Applications { get; set; } =
        new List<OpenWithApplication>();
    internal string? LastFilePath { get; private set; }
    internal FileRevealWindowMode? LastWindowMode { get; private set; }
    internal OpenWithApplication? LastApplication { get; private set; }
    internal int LoadApplicationsCount { get; private set; }
    internal int RevealCount { get; private set; }
    internal int OpenWithCount { get; private set; }
    internal int ChooseApplicationCount { get; private set; }
    internal Exception? ExceptionToThrow { get; set; }
    internal bool BlockApplicationLoading { get; set; }
    internal Task ApplicationLoadingStarted =>
        _applicationLoadingStarted.Task;

    private readonly TaskCompletionSource _applicationLoadingStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<IReadOnlyList<OpenWithApplication>>
        _applicationLoadingCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IReadOnlyList<OpenWithApplication>>
        GetOpenWithApplicationsAsync(
            string filePath,
            CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        ThrowIfRequired();
        LastFilePath = filePath;
        LoadApplicationsCount++;

        if (BlockApplicationLoading)
        {
            _applicationLoadingStarted.TrySetResult();

            return _applicationLoadingCompletion.Task.WaitAsync(ct);
        }

        return Task.FromResult(Applications);
    }

    public Task RevealInFolderAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        ThrowIfRequired();
        LastFilePath = filePath;
        LastWindowMode = windowMode;
        RevealCount++;

        return Task.CompletedTask;
    }

    public Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(application);
        ct.ThrowIfCancellationRequested();
        ThrowIfRequired();
        LastFilePath = filePath;
        LastApplication = application;
        OpenWithCount++;

        return Task.CompletedTask;
    }

    public Task ChooseApplicationAsync(
        string filePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        ThrowIfRequired();
        LastFilePath = filePath;
        ChooseApplicationCount++;

        return Task.CompletedTask;
    }

    internal void CompleteApplicationLoading()
    {
        _applicationLoadingCompletion.TrySetResult(Applications);
    }

    private void ThrowIfRequired()
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
}

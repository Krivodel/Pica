using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class ReadyImagePresentationReadiness :
    IImagePresentationReadiness
{
    public bool IsReady { get; set; } = true;

    internal int WaitCount { get; private set; }

    public Task WaitAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        WaitCount++;

        return Task.CompletedTask;
    }
}

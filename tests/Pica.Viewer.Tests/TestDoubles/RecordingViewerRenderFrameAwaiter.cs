using Avalonia.Threading;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingViewerRenderFrameAwaiter :
    IViewerRenderFrameAwaiter
{
    internal int WaitCount { get; private set; }
    internal bool WaitHasUiThreadAccess { get; private set; }

    public Task WaitAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        WaitCount++;
        WaitHasUiThreadAccess = Dispatcher.UIThread.CheckAccess();

        return Task.CompletedTask;
    }
}

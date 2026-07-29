namespace Pica.Viewer.Services;

internal interface IViewerRenderFrameAwaiter
{
    Task WaitAsync(CancellationToken ct);
}

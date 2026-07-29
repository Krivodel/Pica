namespace Pica.Viewer.Services;

internal interface IImagePresentationReadiness
{
    bool IsReady { get; }

    Task WaitAsync(CancellationToken ct);
}

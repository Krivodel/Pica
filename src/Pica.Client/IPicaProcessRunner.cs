using Pica.Protocol;

namespace Pica.Client;

public interface IPicaProcessRunner
{
    Task RunAsync(
        string executablePath,
        PicaViewerRequest request,
        Func<PicaActionInvocation, CancellationToken, Task> invocationHandler,
        CancellationToken ct);
}

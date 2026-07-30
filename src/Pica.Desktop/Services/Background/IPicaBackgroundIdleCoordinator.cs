namespace Pica.Desktop.Services.Background;

internal interface IPicaBackgroundIdleCoordinator
{
    Task<IPicaBackgroundActivation?> Completion { get; }

    void Start(TimeSpan idleTimeout, CancellationToken ct);

    void Cancel();

    Task StopAsync(CancellationToken ct);
}

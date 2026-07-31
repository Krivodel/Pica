namespace Pica.Desktop.Services.Background;

internal interface IPicaBackgroundActivation : IAsyncDisposable
{
    IReadOnlyList<string> Arguments { get; }
    long? SourceWindowHandle { get; }

    Task AcknowledgeAsync(CancellationToken ct);
}

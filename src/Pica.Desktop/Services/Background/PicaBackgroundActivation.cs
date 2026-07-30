using System.IO.Pipes;

using Pica.Protocol;

namespace Pica.Desktop.Services.Background;

internal sealed class PicaBackgroundActivation : IPicaBackgroundActivation
{
    public IReadOnlyList<string> Arguments { get; }

    private readonly NamedPipeServerStream _pipe;
    private bool _isAcknowledged;

    internal PicaBackgroundActivation(
        IReadOnlyList<string> arguments,
        NamedPipeServerStream pipe)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Arguments = arguments.ToArray();
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
    }

    public async Task AcknowledgeAsync(CancellationToken ct)
    {
        if (_isAcknowledged)
        {
            throw new InvalidOperationException(
                "The Pica background activation has already been acknowledged.");
        }

        await PicaProtocolStream
            .WriteAsync(
                _pipe,
                PicaBackgroundActivationAcknowledgement.Instance,
                ct)
            .ConfigureAwait(false);
        _isAcknowledged = true;
    }

    public async ValueTask DisposeAsync()
    {
        await _pipe.DisposeAsync().ConfigureAwait(false);
    }
}

using Avalonia.Controls;

namespace Pica.Viewer.Views;

internal sealed class ViewerActionGate
{
    internal bool IsRunning => _isRunning;

    private readonly Control _interactionRoot;
    private bool _isRunning;

    internal ViewerActionGate(Control interactionRoot)
    {
        _interactionRoot = interactionRoot
            ?? throw new ArgumentNullException(nameof(interactionRoot));
    }

    internal async Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct,
        bool blockWindowInteraction = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_isRunning)
        {
            return;
        }

        _isRunning = true;

        if (blockWindowInteraction)
        {
            _interactionRoot.IsHitTestVisible = false;
        }

        try
        {
            await operation(ct);
        }
        finally
        {
            if (blockWindowInteraction)
            {
                _interactionRoot.IsHitTestVisible = true;
            }

            _isRunning = false;
        }
    }
}

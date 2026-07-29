namespace Pica.Viewer.Services;

internal sealed class OperationCancellation
{
    internal CancellationToken Token { get; }
    internal bool IsCancellationRequested =>
        Token.IsCancellationRequested;

    private readonly CancellationTokenSource _source;
    private readonly object _sync = new();
    private bool _isCancellationInProgress;
    private bool _isCompleted;
    private bool _isDisposed;

    internal OperationCancellation()
    {
        _source = new CancellationTokenSource();
        Token = _source.Token;
    }

    internal void Cancel()
    {
        lock (_sync)
        {
            if (_isCompleted
                || _isCancellationInProgress)
            {
                return;
            }

            _isCancellationInProgress = true;
        }

        try
        {
            _source.Cancel();
        }
        finally
        {
            bool shouldDispose;

            lock (_sync)
            {
                _isCancellationInProgress = false;
                shouldDispose = _isCompleted
                    && MarkDisposed();
            }

            if (shouldDispose)
            {
                _source.Dispose();
            }
        }
    }

    internal void Complete()
    {
        bool shouldDispose;

        lock (_sync)
        {
            _isCompleted = true;
            shouldDispose = !_isCancellationInProgress
                && MarkDisposed();
        }

        if (shouldDispose)
        {
            _source.Dispose();
        }
    }

    private bool MarkDisposed()
    {
        if (_isDisposed)
        {
            return false;
        }

        _isDisposed = true;

        return true;
    }
}

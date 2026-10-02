using Microsoft.Extensions.Logging;

using Pica.Viewer.Controls;

namespace Pica.Viewer.Services;

public sealed class ViewerRecordedSettingContribution<TValue> : ViewerSettingContribution
    where TValue : notnull
{
    public TValue InitialValue { get; }

    private readonly Func<nint, CancellationToken, Task<TValue>> _recordAsync;
    private readonly Func<TValue, CancellationToken, Task> _applyAsync;
    private readonly Func<TValue, string> _format;
    private readonly Func<Exception, string> _getErrorMessage;
    private readonly ILogger _logger;
    private readonly Func<TValue>? _getCurrentValue;

    public ViewerRecordedSettingContribution(
        string label,
        TValue initialValue,
        Func<nint, CancellationToken, Task<TValue>> recordAsync,
        Func<TValue, CancellationToken, Task> applyAsync,
        Func<TValue, string> format,
        Func<Exception, string> getErrorMessage,
        ILogger logger,
        Func<TValue>? getCurrentValue = null)
        : base(label)
    {
        ArgumentNullException.ThrowIfNull(initialValue);
        InitialValue = initialValue;
        _recordAsync = recordAsync ?? throw new ArgumentNullException(nameof(recordAsync));
        _applyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _getErrorMessage = getErrorMessage ?? throw new ArgumentNullException(nameof(getErrorMessage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getCurrentValue = getCurrentValue;
    }

    public async Task<TValue> RecordAsync(nint owner, CancellationToken ct)
    {
        return await _recordAsync(owner, ct).ConfigureAwait(false);
    }

    public async Task ApplyAsync(TValue value, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(value);
        await _applyAsync(value, ct).ConfigureAwait(false);
    }

    internal override ViewerSettingControl CreateControl()
    {
        return new ViewerRecordedSettingControl<TValue>(
            Label, InitialValue, RecordAsync, ApplyAsync, _format, _getErrorMessage, _logger, _getCurrentValue);
    }
}

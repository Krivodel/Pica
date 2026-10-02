using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace Pica.Viewer.Controls;

internal sealed class ViewerRecordedSettingControl<TValue> : ViewerSettingControl
    where TValue : notnull
{
    internal override Control Control => _panel;
    internal Button RecordButton { get; }
    internal TextBlock ValueText { get; }
    internal TextBlock ErrorText => _error;

    private const double RowSpacing = 8d;
    private const string ChangeButtonText = "Изменить";
    private const string RecordingButtonText = "Нажми сочетание клавиш";
    private readonly StackPanel _panel;
    private readonly Func<nint, CancellationToken, Task<TValue>> _recordAsync;
    private readonly Func<TValue, string> _format;
    private readonly ViewerSettingErrorControl _error;
    private readonly IAsyncRelayCommand<TValue> _applyCommand;
    private readonly Func<TValue>? _getCurrentValue;
    private readonly List<Visual> _ancestors = [];
    private CancellationTokenSource? _recording;
    private TopLevel? _owner;
    private TValue _currentValue;
    private bool _isApplying;

    internal ViewerRecordedSettingControl(
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
        _currentValue = initialValue;
        _recordAsync = recordAsync;
        _format = format;
        _error = new ViewerSettingErrorControl(getErrorMessage, logger);
        _getCurrentValue = getCurrentValue;
        _applyCommand = new AsyncRelayCommand<TValue>(async (value, ct) =>
        {
            ArgumentNullException.ThrowIfNull(value);
            using CancellationTokenSource applyCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                ct, _recording?.Token ?? CancellationToken.None);
            await applyAsync(value, applyCancellation.Token);
        });
        RecordButton = new Button { Content = ChangeButtonText };
        ValueText = new TextBlock
        {
            Text = format(initialValue),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, RowSpacing, 0d)
        };
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(RecordButton, 1);
        row.Children.Add(ValueText);
        row.Children.Add(RecordButton);
        _panel = new StackPanel { Spacing = ErrorSpacing };
        _panel.Children.Add(row);
        _panel.Children.Add(ErrorText);
        RecordButton.Click += OnRecordClick;
        RecordButton.LostFocus += OnRecordLostFocus;
        _panel.AttachedToVisualTree += OnAttached;
        _panel.DetachedFromVisualTree += OnDetached;
    }

    internal async Task RecordAsync(nint ownerHandle, CancellationToken ct)
    {
        if (_recording is not null)
        {
            _recording.Cancel();
            return;
        }

        using CancellationTokenSource recording = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _recording = recording;
        _owner?.SetValue(ViewerSettingRecording.IsActiveProperty, true);
        RecordButton.Content = RecordingButtonText;
        _error.ClearError();

        try
        {
            TValue value = await _recordAsync(ownerHandle, recording.Token);
            recording.Token.ThrowIfCancellationRequested();
            _isApplying = true;
            RecordButton.IsEnabled = false;
            await _applyCommand.ExecuteAsync(value);
            _currentValue = value;
            ValueText.Text = _format(_currentValue);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _error.ShowError(ex);
        }
        finally
        {
            _owner?.ClearValue(ViewerSettingRecording.IsActiveProperty);
            _recording = null;
            _isApplying = false;
            RecordButton.Content = ChangeButtonText;
            RecordButton.IsEnabled = true;
            RefreshValue();
        }
    }

    private void RefreshValue()
    {
        if ((_getCurrentValue is not null) && (_recording is null))
        {
            _currentValue = _getCurrentValue();
            ValueText.Text = _format(_currentValue);
        }
    }

    private async void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        await RecordAsync(_owner?.TryGetPlatformHandle()?.Handle ?? nint.Zero, CancellationToken.None);
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _owner = TopLevel.GetTopLevel(_panel);
        _owner?.AddHandler(InputElement.KeyDownEvent, OnOwnerKeyDown, RoutingStrategies.Tunnel, true);

        if (_owner is Window window)
        {
            window.Deactivated += OnOwnerDeactivated;
            window.Closed += OnOwnerDeactivated;
            window.Activated += OnOwnerActivated;
        }

        _ancestors.AddRange(_panel.GetVisualAncestors());
        RefreshValue();

        foreach (Visual ancestor in _ancestors)
        {
            ancestor.PropertyChanged += OnAncestorChanged;
        }
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _recording?.Cancel();
        _owner?.ClearValue(ViewerSettingRecording.IsActiveProperty);
        _owner?.RemoveHandler(InputElement.KeyDownEvent, OnOwnerKeyDown);

        if (_owner is Window window)
        {
            window.Deactivated -= OnOwnerDeactivated;
            window.Closed -= OnOwnerDeactivated;
            window.Activated -= OnOwnerActivated;
        }

        foreach (Visual ancestor in _ancestors)
        {
            ancestor.PropertyChanged -= OnAncestorChanged;
        }

        _ancestors.Clear();
        _owner = null;
    }

    private void OnOwnerDeactivated(object? sender, EventArgs e)
    {
        _recording?.Cancel();
    }

    private void OnOwnerKeyDown(object? sender, KeyEventArgs e)
    {
        if ((_recording is not null) && (e.Key == Key.Escape))
        {
            _recording.Cancel();
            e.Handled = true;
        }
    }

    private void OnRecordLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isApplying)
        {
            _recording?.Cancel();
        }
    }

    private void OnOwnerActivated(object? sender, EventArgs e)
    {
        RefreshValue();
    }

    private void OnAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (((e.Property == Visual.IsVisibleProperty) || (e.Property == Avalonia.Input.InputElement.IsHitTestVisibleProperty))
            && (e.NewValue is false))
        {
            _recording?.Cancel();
        }
        else if ((e.Property == Visual.IsVisibleProperty) && (e.NewValue is true))
        {
            RefreshValue();
        }
    }
}

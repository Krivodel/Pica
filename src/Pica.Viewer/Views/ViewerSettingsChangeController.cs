using System.ComponentModel;

using Avalonia;

using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerSettingsChangeController : IDisposable
{
    private readonly ImageViewerView _view;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ViewerPointerInputController _pointerInput;
    private readonly ViewerWindowModeController _windowMode;

    internal ViewerSettingsChangeController(
        ImageViewerView view,
        ImageViewerSettingsViewModel settings,
        ImageViewportController viewport,
        ViewerPointerInputController pointerInput,
        ViewerWindowModeController windowMode)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _pointerInput = pointerInput
            ?? throw new ArgumentNullException(nameof(pointerInput));
        _windowMode = windowMode
            ?? throw new ArgumentNullException(nameof(windowMode));
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    private void ApplyFreeZoomOutChange()
    {
        if (_settings.AllowFreeZoomOut
            || !_viewport.TryGetResetImagePlacement(
                out double fittedScale,
                out _,
                out _)
            || (_viewport.Scale >= fittedScale))
        {
            return;
        }

        Size viewport = _viewport.GetViewportSize();
        _viewport.BeginScaleAnimation(
            fittedScale,
            new Point(
                viewport.Width / 2d,
                viewport.Height / 2d));
    }

    private void ApplyRememberWindowPlacementChange()
    {
        if (_settings.RememberWindowPlacement)
        {
            _windowMode.CapturePlacement();
            return;
        }

        _windowMode.ResetRememberedPlacement();
    }

    private void OnSettingsPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.IsFilteringEnabled),
            StringComparison.Ordinal))
        {
            _view.ApplyImageFiltering(
                _settings.IsFilteringEnabled);
            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.ExpandOnDoubleClick),
            StringComparison.Ordinal))
        {
            _pointerInput.ResetDoubleClickTracking();
            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.AllowFreeZoomOut),
            StringComparison.Ordinal))
        {
            ApplyFreeZoomOutChange();
            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.IsPanningInertiaEnabled),
            StringComparison.Ordinal))
        {
            _viewport.ResetPanMotion();
            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.ResizeBehavior),
            StringComparison.Ordinal))
        {
            if (_windowMode.ShouldFitWindowToCurrentImage())
            {
                _windowMode.FitWindowToCurrentImage();
            }

            return;
        }

        if (string.Equals(
            e.PropertyName,
            nameof(ImageViewerSettingsViewModel.RememberWindowPlacement),
            StringComparison.Ordinal))
        {
            ApplyRememberWindowPlacementChange();
        }
    }
}

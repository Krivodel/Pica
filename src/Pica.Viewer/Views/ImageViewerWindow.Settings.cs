using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using SukiUI.Controls;

using Pica.Viewer.Controls;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private const double ImageInformationSettingsTopSpacing = 10d;

    private void OnFloatingMenuPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        e.Handled = true;
    }

    private IReadOnlyList<ViewerSettingControl> CreateSettingControls()
    {
        List<ViewerSettingControl> settingControls =
        [
            new ViewerChoiceSettingControl<int>(
                "Скорость перемещения",
                ViewerSettingChoices.SpeedOptions,
                _settings.MovementSpeed,
                ChangeMovementSpeedAsync),
            new ViewerCheckBoxSettingControl(
                "Инерция перемещения",
                _settings.IsPanningInertiaEnabled,
                ChangePanningInertiaAsync),
            new ViewerChoiceSettingControl<int>(
                "Скорость масштабирования",
                ViewerSettingChoices.SpeedOptions,
                _settings.ZoomSpeed,
                ChangeZoomSpeedAsync),
            new ViewerCheckBoxSettingControl(
                "Свободное отдаление",
                _settings.AllowFreeZoomOut,
                ChangeAllowFreeZoomOutAsync),
            new ViewerChoiceSettingControl<WindowResizeBehavior>(
                "Изменение размера окна",
                ViewerSettingChoices.ResizeBehaviorOptions,
                _settings.ResizeBehavior,
                ChangeResizeBehaviorAsync),
            new ViewerCheckBoxSettingControl(
                "Разворачивать двойным щелчком",
                _settings.ExpandOnDoubleClick,
                ChangeExpandOnDoubleClickAsync),
            new ViewerCheckBoxSettingControl(
                "Запоминать положение и размер окна",
                _settings.RememberWindowPlacement,
                ChangeRememberWindowPlacementAsync),
            new ViewerCheckBoxSettingControl(
                "Быстрая загрузка",
                _settings.IsFastLoadingEnabled,
                ChangeFastLoadingAsync),
            new ViewerCheckBoxSettingControl(
                "Показывать название",
                _settings.ShowImageName,
                ChangeShowImageNameAsync,
                topSpacing: ImageInformationSettingsTopSpacing),
            new ViewerCheckBoxSettingControl(
                "Показывать формат",
                _settings.ShowImageFormat,
                ChangeShowImageFormatAsync),
            new ViewerCheckBoxSettingControl(
                "Показывать разрешение",
                _settings.ShowImageResolution,
                ChangeShowImageResolutionAsync),
            new ViewerCheckBoxSettingControl(
                "Показывать дату изменения",
                _settings.ShowImageModificationDate,
                ChangeShowImageModificationDateAsync)
        ];

        return settingControls;
    }

    private async Task ToggleFilteringAsync()
    {
        _settings.IsFilteringEnabled = !_settings.IsFilteringEnabled;
        _view.ApplyImageFiltering(_settings.IsFilteringEnabled);
        _view.UpdateFilteringMenuState(_settings.IsFilteringEnabled);
        await SaveCurrentStateAsync();
    }

    private async Task ChangeMovementSpeedAsync(int movementSpeed)
    {
        _settings.MovementSpeed = movementSpeed;
        await SaveCurrentStateAsync();
    }

    private async Task ChangeZoomSpeedAsync(int zoomSpeed)
    {
        _settings.ZoomSpeed = zoomSpeed;
        await SaveCurrentStateAsync();
    }

    private async Task ChangeExpandOnDoubleClickAsync(bool expandOnDoubleClick)
    {
        _settings.ExpandOnDoubleClick = expandOnDoubleClick;
        _imageDoubleClickTracker.Reset();
        await SaveCurrentStateAsync();
    }

    private async Task ChangeFastLoadingAsync(bool isFastLoadingEnabled)
    {
        _settings.IsFastLoadingEnabled = isFastLoadingEnabled;

        if (_settings.IsFastLoadingEnabled)
        {
            StartPreviewCachePriming();
        }
        else
        {
            if (_previewCachePrimingTask is not null)
            {
                CancelPendingImageLoad();
            }

            _previewCache.Clear();
        }

        await SaveCurrentStateAsync();
    }

    private async Task ChangeAllowFreeZoomOutAsync(bool allowFreeZoomOut)
    {
        _settings.AllowFreeZoomOut = allowFreeZoomOut;

        if (!_settings.AllowFreeZoomOut
            && TryGetResetImagePlacement(out double fittedScale, out _, out _)
            && (_scale < fittedScale))
        {
            Size viewport = GetViewportSize();
            BeginScaleAnimation(
                fittedScale,
                new Point(viewport.Width / 2d, viewport.Height / 2d));
        }

        await SaveCurrentStateAsync();
    }

    private async Task ChangePanningInertiaAsync(bool isPanningInertiaEnabled)
    {
        _settings.IsPanningInertiaEnabled = isPanningInertiaEnabled;

        ResetPanMotion();
        await SaveCurrentStateAsync();
    }

    private async Task ChangeResizeBehaviorAsync(WindowResizeBehavior resizeBehavior)
    {
        _settings.ResizeBehavior = resizeBehavior;

        if (ShouldFitWindowToCurrentImage())
        {
            FitWindowToCurrentImage();
        }

        await SaveCurrentStateAsync();
    }

    private async Task ChangeRememberWindowPlacementAsync(bool rememberWindowPlacement)
    {
        _settings.RememberWindowPlacement = rememberWindowPlacement;

        if (_settings.RememberWindowPlacement)
        {
            CaptureWindowedPlacement();
        }
        else
        {
            ResetRememberedWindowPlacement();
        }

        await SaveCurrentStateAsync();
    }

    private Task ChangeShowImageNameAsync(bool showImageName)
    {
        _settings.ShowImageName = showImageName;

        return ApplyImageInformationSettingsAsync();
    }

    private Task ChangeShowImageFormatAsync(bool showImageFormat)
    {
        _settings.ShowImageFormat = showImageFormat;

        return ApplyImageInformationSettingsAsync();
    }

    private Task ChangeShowImageResolutionAsync(bool showImageResolution)
    {
        _settings.ShowImageResolution = showImageResolution;

        return ApplyImageInformationSettingsAsync();
    }

    private Task ChangeShowImageModificationDateAsync(
        bool showImageModificationDate)
    {
        _settings.ShowImageModificationDate = showImageModificationDate;

        return ApplyImageInformationSettingsAsync();
    }

    private async Task ApplyImageInformationSettingsAsync()
    {
        UpdateSelectedImageInformation();
        await SaveCurrentStateAsync();
    }

    private async Task SaveCurrentStateAsync()
    {
        await _imageViewerStateService.SaveAsync(CreateState(), CancellationToken.None);
    }

    private void ShowSettingsPanel()
    {
        _view.SettingsPanel.IsVisible = true;
        _view.SettingsPanel.IsHitTestVisible = true;
        StartSettingsPanelAnimation(
            ImageViewerVisualMetrics.VisibleControlsOpacity,
            0d,
            null);
    }

    private void HideSettingsPanel()
    {
        _view.SettingsPanel.IsHitTestVisible = false;
        StartSettingsPanelAnimation(
            ImageViewerVisualMetrics.HiddenControlsOpacity,
            ImageViewerVisualMetrics.SettingsPanelHiddenOffset,
            CompleteSettingsPanelHide);
    }

    private void HideSettingsPanelImmediately()
    {
        _settingsPanelAnimationId++;
        _view.SettingsPanel.IsHitTestVisible = false;
        _view.SettingsPanel.IsVisible = false;
        _view.SettingsPanel.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;

        if (_view.SettingsPanel.RenderTransform is TranslateTransform transform)
        {
            transform.Y = ImageViewerVisualMetrics.SettingsPanelHiddenOffset;
        }
    }

    private void StartSettingsPanelAnimation(
        double targetOpacity,
        double targetOffset,
        Action? completed)
    {
        if (_view.SettingsPanel.RenderTransform is not TranslateTransform transform)
        {
            return;
        }

        long animationId = ++_settingsPanelAnimationId;
        double startOpacity = _view.SettingsPanel.Opacity;
        double startOffset = transform.Y;

        StartFrameAnimation(
            SettingsPanelAnimationDuration,
            () => animationId == _settingsPanelAnimationId,
            progress =>
            {
                double easedProgress = EaseOutCubic(progress);
                _view.SettingsPanel.Opacity = startOpacity
                    + ((targetOpacity - startOpacity) * easedProgress);
                transform.Y = startOffset + ((targetOffset - startOffset) * easedProgress);
            },
            completed: completed);
    }

    private void CompleteSettingsPanelHide()
    {
        _view.SettingsPanel.IsVisible = false;
    }

    private void ShowCursor()
    {
        _cursorTimer.Stop();

        if (IsAreaSelectionModeActive())
        {
            return;
        }

        SetVisibleCursor(ViewerCursors.Arrow);

        if (_view.ViewerArea.IsPointerOver && !_view.SettingsPanel.IsPointerOver)
        {
            _cursorTimer.Start();
        }
    }

    private void HideCursor()
    {
        _isCursorHidden = true;
        Cursor = ViewerCursors.Hidden;
    }

    private void SetVisibleCursor(Cursor cursor)
    {
        _isCursorHidden = false;
        Cursor = cursor;
    }

    private bool IsAreaSelectionModeActive()
    {
        return _isSelectionActive || _isSelecting || _isSelectionArmed;
    }

    private void CloseWithFade()
    {
        Close();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ImageViewportController
{
    internal Bitmap? CurrentBitmap =>
        _imagePresentation.DisplayedBitmap;
    internal PixelSize CurrentSourcePixelSize =>
        _imagePresentation.SourcePixelSize;
    internal double Scale => _scale;
    internal double OffsetX => _offsetX;
    internal double OffsetY => _offsetY;
    internal bool IsPanning => _isPanning;

    internal event EventHandler? LayoutChanged;

    private const double MinimumScale = 0.05d;
    private const double MaximumScale = 32d;
    private const double ScaleAnimationDurationSeconds = 0.14d;

    private static readonly TimeSpan ScaleAnimationDuration =
        TimeSpan.FromSeconds(ScaleAnimationDurationSeconds);

    private readonly TopLevel _topLevel;
    private readonly ImageViewerView _view;
    private readonly ImagePresentationController _imagePresentation;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ViewerAnimationFrameScheduler _animationFrameScheduler;
    private readonly ViewerFrameAnimationRunner _animationRunner;
    private readonly ImagePanMotion _panMotion = new();
    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;
    private long _scaleAnimationId;
    private bool _isPanning;
    private bool _isPanAnimationFramePending;
    private Point _lastPointerPosition;
    private DateTimeOffset _lastPanAnimationFrameTimestamp;

    internal ImageViewportController(
        TopLevel topLevel,
        ImageViewerView view,
        ImagePresentationController imagePresentation,
        ImageViewerSettingsViewModel settings,
        ViewerAnimationFrameScheduler animationFrameScheduler,
        ViewerFrameAnimationRunner animationRunner)
    {
        _topLevel = topLevel
            ?? throw new ArgumentNullException(nameof(topLevel));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(nameof(imagePresentation));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _animationFrameScheduler = animationFrameScheduler
            ?? throw new ArgumentNullException(nameof(animationFrameScheduler));
        _animationRunner = animationRunner
            ?? throw new ArgumentNullException(nameof(animationRunner));
    }

    internal void ResetScaleAndCenter()
    {
        if (!TryGetResetImagePlacement(
            out double targetScale,
            out double targetOffsetX,
            out double targetOffsetY))
        {
            return;
        }

        _scale = targetScale;
        _offsetX = targetOffsetX;
        _offsetY = targetOffsetY;
        ApplyImageLayout();
        ResetPanMotion();
    }

    internal void BeginResetScaleAndCenterAnimation()
    {
        ResetPanMotion();

        if (!TryGetResetImagePlacement(
            out double targetScale,
            out double targetOffsetX,
            out double targetOffsetY))
        {
            return;
        }

        double startScale = _scale;
        double startOffsetX = _offsetX;
        double startOffsetY = _offsetY;

        StartScaleFrameAnimation(
            easedProgress =>
            {
                _scale = startScale
                    + ((targetScale - startScale) * easedProgress);
                _offsetX = startOffsetX
                    + ((targetOffsetX - startOffsetX) * easedProgress);
                _offsetY = startOffsetY
                    + ((targetOffsetY - startOffsetY) * easedProgress);
                ApplyImageLayout();
            });
    }

    internal bool TryGetResetImagePlacement(
        out double targetScale,
        out double targetOffsetX,
        out double targetOffsetY)
    {
        targetScale = _scale;
        targetOffsetX = _offsetX;
        targetOffsetY = _offsetY;

        if (CurrentBitmap is null)
        {
            return false;
        }

        Size viewport = GetViewportSize();

        if ((viewport.Width <= 0d) || (viewport.Height <= 0d))
        {
            return false;
        }

        double viewportPixelWidth =
            viewport.Width * _topLevel.RenderScaling;
        double viewportPixelHeight =
            viewport.Height * _topLevel.RenderScaling;
        PixelSize sourcePixelSize = GetCurrentSourcePixelSize();
        double fittedScale = ImageWindowGeometry.CalculateFittedScale(
            sourcePixelSize,
            new Size(viewportPixelWidth, viewportPixelHeight));
        targetScale = sourcePixelSize.Width
            * fittedScale
            / CurrentBitmap.PixelSize.Width;
        double targetImageWidth = CurrentBitmap.PixelSize.Width
            * targetScale
            / _topLevel.RenderScaling;
        double targetImageHeight = CurrentBitmap.PixelSize.Height
            * targetScale
            / _topLevel.RenderScaling;
        targetOffsetX = (viewport.Width - targetImageWidth) / 2d;
        targetOffsetY = (viewport.Height - targetImageHeight) / 2d;

        return true;
    }

    internal PixelSize GetCurrentSourcePixelSize()
    {
        if (CurrentSourcePixelSize is { Width: > 0, Height: > 0 })
        {
            return CurrentSourcePixelSize;
        }

        return CurrentBitmap?.PixelSize ?? new PixelSize();
    }

    internal void ApplyImageLayout()
    {
        if (CurrentBitmap is null)
        {
            return;
        }

        _view.Image.Width = GetImageDipWidth();
        _view.Image.Height = GetImageDipHeight();
        ClampImageOffset();
        Canvas.SetLeft(_view.Image, _offsetX);
        Canvas.SetTop(_view.Image, _offsetY);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    internal bool TryGetCurrentPanBounds(out Rect bounds)
    {
        Size imageSize = new(
            GetImageDipWidth(),
            GetImageDipHeight());
        Size viewportSize = GetViewportSize();
        bounds = new Rect();

        if ((imageSize.Width <= 0d)
            || (imageSize.Height <= 0d)
            || (viewportSize.Width <= 0d)
            || (viewportSize.Height <= 0d))
        {
            return false;
        }

        bounds = ImageWindowGeometry.GetPanBounds(
            imageSize,
            viewportSize);

        return true;
    }

    internal double GetImageDipWidth()
    {
        return CurrentBitmap is null
            ? 0d
            : CurrentBitmap.PixelSize.Width
                * _scale
                / _topLevel.RenderScaling;
    }

    internal double GetImageDipHeight()
    {
        return CurrentBitmap is null
            ? 0d
            : CurrentBitmap.PixelSize.Height
                * _scale
                / _topLevel.RenderScaling;
    }

    internal Size GetViewportSize()
    {
        return _view.ViewerArea.Bounds.Size;
    }

    internal Rect GetVisibleImageRect()
    {
        Rect windowRect = new(
            0d,
            0d,
            _view.ViewerArea.Bounds.Width,
            _view.ViewerArea.Bounds.Height);
        Rect imageRect = new(
            _offsetX,
            _offsetY,
            GetImageDipWidth(),
            GetImageDipHeight());

        return imageRect.Intersect(windowRect);
    }

    internal void BeginScaleAnimation(
        double targetScale,
        Point anchor)
    {
        if (CurrentBitmap is null)
        {
            return;
        }

        ResetPanMotion();
        double startScale = _scale;
        double minimumScale = MinimumScale;
        double maximumScale = MaximumScale;

        if (TryGetResetImagePlacement(
            out double fittedScale,
            out _,
            out _))
        {
            maximumScale = Math.Max(
                maximumScale,
                fittedScale);

            if (!_settings.AllowFreeZoomOut)
            {
                minimumScale = fittedScale;
            }
        }

        double clampedScale = Math.Clamp(
            targetScale,
            minimumScale,
            maximumScale);
        double imageX =
            (anchor.X - _offsetX) / GetImageDipWidth();
        double imageY =
            (anchor.Y - _offsetY) / GetImageDipHeight();

        if (_isPanning)
        {
            StopScaleAnimation();
            ApplyScaleAtAnchor(
                clampedScale,
                anchor,
                imageX,
                imageY);
            ResetPanMotion();
            return;
        }

        StartScaleFrameAnimation(
            easedProgress =>
            {
                double frameScale = startScale
                    + ((clampedScale - startScale) * easedProgress);
                ApplyScaleAtAnchor(
                    frameScale,
                    anchor,
                    imageX,
                    imageY);
            });
    }

    internal void ApplyScaleAfterSourceReplacement(double scale)
    {
        _scale = scale;
        ApplyImageLayout();
        ResetPanMotion();
    }

    internal void StopScaleAnimation()
    {
        _scaleAnimationId++;
    }

    internal void BeginPanMotion(Point pointerPosition)
    {
        _isPanning = true;
        StopScaleAnimation();
        ApplyImageLayout();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        _lastPointerPosition = pointerPosition;
        _lastPanAnimationFrameTimestamp = timestamp;
        _panMotion.Begin(
            new Point(_offsetX, _offsetY),
            timestamp);
    }

    internal void MovePanMotion(
        Point pointerPosition,
        KeyModifiers modifiers)
    {
        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            return;
        }

        Vector pointerDelta =
            pointerPosition - _lastPointerPosition;
        int multiplier = GetEffectiveMovementSpeed(modifiers);
        Vector imageDelta = pointerDelta * multiplier;
        _lastPointerPosition = pointerPosition;
        _panMotion.Move(
            imageDelta,
            bounds,
            DateTimeOffset.UtcNow);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();
    }

    internal bool ReleasePanMotion()
    {
        bool wasPanning = _isPanning;
        _isPanning = false;

        if (!wasPanning)
        {
            return false;
        }

        _panMotion.Release(
            GetPanMotionMode(),
            DateTimeOffset.UtcNow);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();

        return true;
    }

    internal void ResetPanMotion()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        Point offset = new(_offsetX, _offsetY);
        _lastPanAnimationFrameTimestamp = timestamp;

        if (_isPanning)
        {
            _panMotion.Begin(offset, timestamp);
            return;
        }

        _panMotion.Reset(offset);
    }

    internal void StopPanMotion()
    {
        _isPanning = false;
        _isPanAnimationFramePending = false;
        _panMotion.Reset(new Point(_offsetX, _offsetY));
    }

    private int GetEffectiveMovementSpeed(
        KeyModifiers modifiers)
    {
        bool isBaseSpeedRequested =
            (modifiers
                & (KeyModifiers.Shift | KeyModifiers.Alt))
            != KeyModifiers.None;

        return isBaseSpeedRequested
            ? ViewerSettingsDefaults.MinimumSpeed
            : _settings.MovementSpeed;
    }

    private ImagePanMotionMode GetPanMotionMode()
    {
        return _settings.IsPanningInertiaEnabled
            ? ImagePanMotionMode.SmoothWithInertia
            : ImagePanMotionMode.Smooth;
    }

    private void ClampImageOffset()
    {
        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            return;
        }

        Point offset = ImageWindowGeometry.ClampOffset(
            new Point(_offsetX, _offsetY),
            bounds);
        _offsetX = offset.X;
        _offsetY = offset.Y;
    }

    private void StartScaleFrameAnimation(
        Action<double> applyFrame)
    {
        long animationId = ++_scaleAnimationId;

        _animationRunner.Start(
            ScaleAnimationDuration,
            () => animationId == _scaleAnimationId,
            progress => applyFrame(
                ViewerFrameAnimationRunner.EaseOutCubic(progress)));
    }

    private void ApplyScaleAtAnchor(
        double scale,
        Point anchor,
        double imageX,
        double imageY)
    {
        _scale = scale;
        _offsetX = anchor.X
            - (imageX * GetImageDipWidth());
        _offsetY = anchor.Y
            - (imageY * GetImageDipHeight());
        ApplyImageLayout();
    }

    private void ApplyPanMotionOffset()
    {
        _offsetX = _panMotion.CurrentOffset.X;
        _offsetY = _panMotion.CurrentOffset.Y;
        ApplyImageLayout();
    }

    private void SchedulePanAnimationFrame()
    {
        if (!_panMotion.IsActive
            || _isPanAnimationFramePending)
        {
            return;
        }

        _isPanAnimationFramePending = true;
        _animationFrameScheduler.RequestAnimationFrame(
            OnPanAnimationFrame);
    }

    private void OnPanAnimationFrame(TimeSpan frameTime)
    {
        _ = frameTime;
        _isPanAnimationFramePending = false;

        if (!_panMotion.IsActive)
        {
            return;
        }

        if (!TryGetCurrentPanBounds(out Rect bounds))
        {
            ResetPanMotion();
            return;
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        TimeSpan elapsed =
            timestamp - _lastPanAnimationFrameTimestamp;
        _lastPanAnimationFrameTimestamp = timestamp;
        _panMotion.Advance(
            elapsed,
            GetPanMotionMode(),
            bounds);
        ApplyPanMotionOffset();
        SchedulePanAnimationFrame();
    }
}

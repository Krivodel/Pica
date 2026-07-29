using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Views;

internal sealed class ViewerImagePresentationController :
    IDisposable
{
    private readonly Window _owner;
    private readonly ImageViewerView _view;
    private readonly ImageViewerInformationViewModel _information;
    private readonly ImagePresentationController _imagePresentation;
    private readonly ImageViewerSettingsViewModel _settings;
    private readonly ImageViewportController _viewport;
    private readonly ImageSelectionController _selection;
    private readonly ViewerSelectionInteractionController
        _selectionInteraction;
    private readonly ViewerWindowModeController _windowMode;

    internal ViewerImagePresentationController(
        Window owner,
        ImageViewerView view,
        ImageViewerInformationViewModel information,
        ImagePresentationController imagePresentation,
        ImageViewerSettingsViewModel settings,
        ImageViewportController viewport,
        ImageSelectionController selection,
        ViewerSelectionInteractionController selectionInteraction,
        ViewerWindowModeController windowMode)
    {
        _owner = owner
            ?? throw new ArgumentNullException(nameof(owner));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _information = information
            ?? throw new ArgumentNullException(nameof(information));
        _imagePresentation = imagePresentation
            ?? throw new ArgumentNullException(
                nameof(imagePresentation));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _viewport = viewport
            ?? throw new ArgumentNullException(nameof(viewport));
        _selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        _selectionInteraction = selectionInteraction
            ?? throw new ArgumentNullException(
                nameof(selectionInteraction));
        _windowMode = windowMode
            ?? throw new ArgumentNullException(nameof(windowMode));
        _imagePresentation.Changed += OnDisplayedBitmapChanged;
        _imagePresentation.LoadTransitioned +=
            OnImageLoadTransitioned;
        _information.PropertyChanged += OnImageInformationChanged;
    }

    public void Dispose()
    {
        _imagePresentation.Changed -= OnDisplayedBitmapChanged;
        _imagePresentation.LoadTransitioned -=
            OnImageLoadTransitioned;
        _information.PropertyChanged -= OnImageInformationChanged;
    }

    internal void ApplyInformation()
    {
        _owner.Title = _information.Information;
        _view.UpdateImageInformation(_information.Information);
        _windowMode.UpdateInformationPanelVisibility();
    }

    private static PixelRect ScalePixelRect(
        PixelRect pixelRect,
        PixelSize sourceSize,
        PixelSize targetSize)
    {
        double scaleX =
            (double)targetSize.Width / sourceSize.Width;
        double scaleY =
            (double)targetSize.Height / sourceSize.Height;
        int left = Math.Clamp(
            (int)Math.Floor(pixelRect.X * scaleX),
            0,
            targetSize.Width - 1);
        int top = Math.Clamp(
            (int)Math.Floor(pixelRect.Y * scaleY),
            0,
            targetSize.Height - 1);
        int right = Math.Clamp(
            (int)Math.Ceiling(
                (pixelRect.X + pixelRect.Width) * scaleX),
            left + 1,
            targetSize.Width);
        int bottom = Math.Clamp(
            (int)Math.Ceiling(
                (pixelRect.Y + pixelRect.Height) * scaleY),
            top + 1,
            targetSize.Height);

        return new PixelRect(
            left,
            top,
            right - left,
            bottom - top);
    }

    private void ApplyFullResolutionLayout(
        ImageLoadTransitionEventArgs transition)
    {
        PixelSize previewSize = transition.PreviousPixelSize;
        PixelRect previewSelection = _selection.PixelRect;
        double previewScale = _viewport.Scale;

        if (!transition.WasPreviewDisplayed
            || (previewSize.Width <= 0)
            || (previewSize.Height <= 0))
        {
            _selectionInteraction.Cancel();
            ApplyLoadedImageLayout();
            return;
        }

        if (_selection.IsActive)
        {
            _selectionInteraction.SetPixelRect(
                ScalePixelRect(
                    previewSelection,
                    previewSize,
                    transition.CurrentPixelSize));
        }

        _viewport.ApplyScaleAfterSourceReplacement(
            previewScale
                * previewSize.Width
                / transition.CurrentPixelSize.Width);
    }

    private void ApplyLoadedImageLayout()
    {
        if (_windowMode.IsWindowed
            && (_settings.ResizeBehavior
                == WindowResizeBehavior.AlwaysFitImage))
        {
            _windowMode.FitWindowToCurrentImage();
            return;
        }

        _viewport.ResetScaleAndCenter();
    }

    private void RefreshSelectionAfterDisplayedBitmapChange()
    {
        _selectionInteraction.CancelClipboardPreparation();

        if (_selection.IsActive)
        {
            _selectionInteraction.ScheduleClipboardPreparation();
        }
    }

    private void OnImageLoadTransitioned(
        object? sender,
        ImageLoadTransitionEventArgs e)
    {
        _ = sender;

        switch (e.Kind)
        {
            case ImageLoadTransitionKind.Started:
                _viewport.ResetPanMotion();
                _selectionInteraction.Cancel();
                break;
            case ImageLoadTransitionKind.PreviewApplied:
                _selectionInteraction.Cancel();
                ApplyLoadedImageLayout();
                break;
            case ImageLoadTransitionKind.FullResolutionApplied:
                ApplyFullResolutionLayout(e);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(e),
                    e.Kind,
                    "The image load transition kind is not supported.");
        }
    }

    private void OnDisplayedBitmapChanged(
        object? sender,
        EventArgs e)
    {
        _ = sender;
        _ = e;

        Bitmap? previousBitmap =
            _view.Image.Source as Bitmap;
        Bitmap? displayedBitmap =
            _imagePresentation.DisplayedBitmap;
        _view.Image.Source = displayedBitmap;

        if (!object.ReferenceEquals(
            previousBitmap,
            displayedBitmap))
        {
            RefreshSelectionAfterDisplayedBitmapChange();
        }
    }

    private void OnImageInformationChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyInformation();
    }
}

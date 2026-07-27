using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

using Pica.Viewer.Resources;

namespace Pica.Viewer.Controls;

public sealed class PixelFilteringToggleSwitch : UserControl
{
    public static readonly StyledProperty<bool> IsFilteringEnabledProperty =
        AvaloniaProperty.Register<PixelFilteringToggleSwitch, bool>(
            nameof(IsFilteringEnabled),
            true,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private const double SwitchWidth = 108d;
    private const double SwitchHeight = 44d;
    private const double ThumbWidth = 42d;
    private const double ThumbHeight = 34d;
    private const double ThumbLeftX = 7d;
    private const double ThumbRightX = 59d;
    private const double IconTop = 12d;
    private const double LeftIconX = 18d;
    private const double RightIconX = 70d;
    private const double IconSize = 20d;
    private const double DotSize = 5d;
    private const double SquareSize = 6d;
    private const double ActiveOpacity = 1d;
    private const double InactiveOpacity = 0.32d;

    private static readonly TimeSpan ThumbAnimationDuration = TimeSpan.FromMilliseconds(150);

    private readonly Border _thumb;
    private readonly TranslateTransform _thumbTransform;
    private readonly Canvas _pixelIcon;
    private readonly Canvas _filteredIcon;

    public PixelFilteringToggleSwitch()
    {
        Width = SwitchWidth;
        Height = SwitchHeight;
        MinWidth = SwitchWidth;
        MinHeight = SwitchHeight;
        Cursor = ViewerCursors.Hand;
        Focusable = false;

        _thumbTransform = new TranslateTransform
        {
            Transitions = CreateThumbTransitions()
        };
        _thumb = CreateThumb(_thumbTransform);
        _pixelIcon = CreatePixelIcon();
        _filteredIcon = CreateFilteredIcon();
        Content = CreateContent();

        PointerPressed += OnPointerPressed;
        UpdateVisualState(false);
    }

    public bool IsFilteringEnabled
    {
        get => GetValue(IsFilteringEnabledProperty);
        set => SetValue(IsFilteringEnabledProperty, value);
    }

    public void Toggle()
    {
        IsFilteringEnabled = !IsFilteringEnabled;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsFilteringEnabledProperty)
        {
            UpdateVisualState(true);
        }
    }

    private Control CreateContent()
    {
        Canvas canvas = new()
        {
            Width = SwitchWidth,
            Height = SwitchHeight,
            ClipToBounds = true
        };

        Border background = new()
        {
            Width = SwitchWidth,
            Height = SwitchHeight,
            Background = new SolidColorBrush(Color.FromArgb(150, 16, 16, 16)),
            CornerRadius = new CornerRadius(8d)
        };
        Border separator = new()
        {
            Width = 1d,
            Height = 18d,
            Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
        };

        Canvas.SetLeft(_thumb, ThumbLeftX);
        Canvas.SetTop(_thumb, (SwitchHeight - ThumbHeight) / 2d);
        Canvas.SetLeft(_pixelIcon, LeftIconX);
        Canvas.SetTop(_pixelIcon, IconTop);
        Canvas.SetLeft(separator, (SwitchWidth - 1d) / 2d);
        Canvas.SetTop(separator, (SwitchHeight - separator.Height) / 2d);
        Canvas.SetLeft(_filteredIcon, RightIconX);
        Canvas.SetTop(_filteredIcon, IconTop);

        canvas.Children.Add(background);
        canvas.Children.Add(_thumb);
        canvas.Children.Add(_pixelIcon);
        canvas.Children.Add(separator);
        canvas.Children.Add(_filteredIcon);

        return canvas;
    }

    private static Border CreateThumb(TranslateTransform transform)
    {
        return new Border
        {
            Width = ThumbWidth,
            Height = ThumbHeight,
            Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(7d),
            IsHitTestVisible = false,
            RenderTransform = transform
        };
    }

    private static Canvas CreatePixelIcon()
    {
        Canvas icon = CreateIconCanvas();

        AddSquare(icon, 2d, 2d);
        AddSquare(icon, 12d, 2d);
        AddSquare(icon, 2d, 12d);
        AddSquare(icon, 12d, 12d);

        return icon;
    }

    private static Canvas CreateFilteredIcon()
    {
        Canvas icon = CreateIconCanvas();

        AddPull(icon, 6.5d, 4.5d, 7d, 3d);
        AddPull(icon, 6.5d, 14.5d, 7d, 3d);
        AddPull(icon, 4.5d, 6.5d, 3d, 7d);
        AddPull(icon, 14.5d, 6.5d, 3d, 7d);
        AddDot(icon, 3d, 3d);
        AddDot(icon, 13d, 3d);
        AddDot(icon, 3d, 13d);
        AddDot(icon, 13d, 13d);

        return icon;
    }

    private static Canvas CreateIconCanvas()
    {
        return new Canvas
        {
            Width = IconSize,
            Height = IconSize,
            IsHitTestVisible = false
        };
    }

    private static void AddSquare(Canvas icon, double x, double y)
    {
        Border square = new()
        {
            Width = SquareSize,
            Height = SquareSize,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(0d)
        };

        Canvas.SetLeft(square, x);
        Canvas.SetTop(square, y);
        icon.Children.Add(square);
    }

    private static void AddDot(Canvas icon, double x, double y)
    {
        Ellipse dot = new()
        {
            Width = DotSize,
            Height = DotSize,
            Fill = Brushes.White
        };

        Canvas.SetLeft(dot, x);
        Canvas.SetTop(dot, y);
        icon.Children.Add(dot);
    }

    private static void AddPull(Canvas icon, double x, double y, double width, double height)
    {
        Border pull = new()
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            CornerRadius = new CornerRadius(Math.Min(width, height) / 2d)
        };

        Canvas.SetLeft(pull, x);
        Canvas.SetTop(pull, y);
        icon.Children.Add(pull);
    }

    private static Transitions CreateThumbTransitions()
    {
        Transitions transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = ThumbAnimationDuration
            }

        ];

        return transitions;
    }

    private void UpdateVisualState(bool animate)
    {
        if (!animate)
        {
            _thumbTransform.Transitions = null;
        }

        _thumbTransform.X = IsFilteringEnabled ? ThumbRightX - ThumbLeftX : 0d;
        _pixelIcon.Opacity = IsFilteringEnabled ? InactiveOpacity : ActiveOpacity;
        _filteredIcon.Opacity = IsFilteringEnabled ? ActiveOpacity : InactiveOpacity;

        if (!animate)
        {
            _thumbTransform.Transitions = CreateThumbTransitions();
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        Toggle();
        e.Handled = true;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Pica.Viewer.Services;

namespace Pica.Viewer.Controls;

public sealed class ViewerSettingsContentControl : ContentControl
{
    public static readonly DirectProperty<ViewerSettingsContentControl, bool> IsRecordingProperty =
        AvaloniaProperty.RegisterDirect<ViewerSettingsContentControl, bool>(nameof(IsRecording), control => control.IsRecording);

    public bool IsRecording
    {
        get => _isRecording;
        private set => SetAndRaise(IsRecordingProperty, ref _isRecording, value);
    }
    public Task Completion => Task.WhenAll(_settings.Select(setting => setting.Completion));

    private const double ControlSpacing = 8d;
    private const double SectionSpacing = 14d;

    private readonly IReadOnlyList<ViewerSettingControl> _settings = Array.Empty<ViewerSettingControl>();
    private Window? _owner;
    private bool _isRecording;

    public ViewerSettingsContentControl(IReadOnlyList<ViewerSettingContribution> settings)
        : this(CreateControls(settings), false)
    {
    }

    internal ViewerSettingsContentControl(IReadOnlyList<ViewerSettingControl> settings, bool includeTitle)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = CreateContent(settings, includeTitle);
    }

    internal static StackPanel CreateContent(IReadOnlyList<ViewerSettingControl> settings, bool includeTitle)
    {
        StackPanel content = new() { Spacing = SectionSpacing };

        if (includeTitle)
        {
            content.Children.Add(new TextBlock
            {
                FontSize = 17d,
                FontWeight = FontWeight.SemiBold,
                Text = "Настройки"
            });
        }

        foreach (ViewerSettingControl setting in settings)
        {
            Control control = setting.Label is { } label
                ? CreateLabeledControl(label, setting.Control)
                : setting.Control;
            content.Children.Add(control);
        }

        return content;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = TopLevel.GetTopLevel(this) as Window;

        if (_owner is not null)
        {
            _owner.Activated += OnOwnerActivated;
            _owner.PropertyChanged += OnOwnerPropertyChanged;
            IsRecording = ViewerSettingRecording.IsActive(_owner);
        }

        RefreshValues();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is not null)
        {
            _owner.Activated -= OnOwnerActivated;
            _owner.PropertyChanged -= OnOwnerPropertyChanged;
            _owner = null;
        }

        IsRecording = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == IsVisibleProperty) && (change.NewValue is true))
        {
            RefreshValues();
        }
    }

    private static IReadOnlyList<ViewerSettingControl> CreateControls(IReadOnlyList<ViewerSettingContribution> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.Select(setting => setting.CreateControl()).ToArray();
    }

    private static StackPanel CreateLabeledControl(string label, Control control)
    {
        StackPanel container = new() { Spacing = ControlSpacing };
        container.Children.Add(new TextBlock { Text = label });
        container.Children.Add(control);

        return container;
    }

    private void RefreshValues()
    {
        foreach (ViewerSettingControl setting in _settings)
        {
            setting.RefreshValue();
        }
    }

    private void OnOwnerActivated(object? sender, EventArgs e)
    {
        RefreshValues();
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ViewerSettingRecording.IsActiveProperty)
        {
            IsRecording = ViewerSettingRecording.IsActive(_owner);
        }
    }
}

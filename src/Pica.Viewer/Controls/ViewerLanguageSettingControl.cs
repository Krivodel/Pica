using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;

using Krivodeling.Localization.Avalonia;
using Pica.Viewer.Behaviors;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Controls;

internal sealed class ViewerLanguageSettingControl : ViewerSettingControl
{
    internal override Control Control => _panel;
    internal override Task Completion => Task.WhenAll(_model.ApplyCommand.ExecutionTask ?? Task.CompletedTask,
        _model.RefreshCommand.ExecutionTask ?? Task.CompletedTask);

    private readonly ViewerLanguageSettingViewModel _model;
    private readonly StackPanel _panel;

    internal ViewerLanguageSettingControl(string label, ViewerLanguageSettingViewModel model, string searchPlaceholderKey)
        : base(label)
    {
        _model = model;
        ComboBox languages = new()
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<ViewerLanguageOptionViewModel>((option, _) => new TextBlock { Text = option?.DisplayName })
        };
        languages.Classes.Add("viewer-language-select");
        ComboBoxSearchBehavior.SetIsEnabled(languages, true);
        languages.Bind(ComboBoxSearchBehavior.TextProperty,
            new Binding(nameof(model.SearchText)) { Mode = BindingMode.TwoWay });
        LocalizationBinding.Bind(languages, ComboBoxSearchBehavior.PlaceholderTextProperty, searchPlaceholderKey);
        languages.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(model.Options)));
        languages.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(model.SelectedOption)) { Mode = BindingMode.TwoWay });
        languages.Bind(Control.IsEnabledProperty, new Binding("!" + nameof(model.IsLoading)));
        languages.DropDownOpened += OnDropDownOpened;
        languages.DropDownClosed += OnDropDownClosed;
        TextBlock error = new() { TextWrapping = TextWrapping.Wrap };
        error.Classes.Add("viewer-error");
        error.Bind(TextBlock.TextProperty, new Binding(nameof(model.ErrorMessage)));
        error.Bind(Control.IsVisibleProperty, new Binding(nameof(model.HasErrorMessage)));
        _panel = new StackPanel { DataContext = model, Spacing = ErrorSpacing };
        _panel.Styles.Add(new StyleInclude(new Uri("avares://Pica.Viewer/"))
        {
            Source = new Uri("avares://Pica.Viewer/Resources/ViewerLanguageSettingStyles.axaml")
        });
        _panel.Children.Add(languages);
        _panel.Children.Add(error);
        _panel.AttachedToVisualTree += (_, _) => _model.Start();
        _panel.DetachedFromVisualTree += (_, _) => _model.Stop();
    }

    internal override void RefreshValue()
    {
        if (!_model.IsLoading)
        {
            _model.RefreshSelection();
        }
    }

    private async void OnDropDownOpened(object? sender, EventArgs e)
    {
        _model.ClearSearchCommand.Execute(null);
        await _model.RefreshCommand.ExecuteAsync(null);
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        _model.ClearSearchCommand.Execute(null);
    }
}

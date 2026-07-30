using Avalonia.Controls;

using Pica.Viewer.Resources;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Controls;

internal sealed partial class ImageViewerToolMenuControl : UserControl
{
    internal Canvas MenuLayer => MenuLayerControl;
    internal Border ToolMenu => ToolMenuControl;
    internal Button CheckerboardBackgroundMenuItem =>
        CheckerboardBackgroundButtonControl;
    internal Button FilteringMenuItem => FilteringButtonControl;
    internal Button ModeMenuButton => ModeMenuButtonControl;
    internal Border ModeMenu => ModeMenuControl;
    internal Button MainModeMenuItem => MainModeButtonControl;
    internal Button ChannelModeMenuItem => ChannelModeButtonControl;

    private Canvas MenuLayerControl =>
        this.FindControl<Canvas>("MenuCanvas")
        ?? throw new InvalidOperationException(
            "The tool menu control is missing its menu layer.");
    private Border ToolMenuControl =>
        this.FindControl<Border>("ToolMenuBorder")
        ?? throw new InvalidOperationException(
            "The tool menu control is missing its primary menu.");
    private Button CheckerboardBackgroundButtonControl =>
        GetRequiredButton("CheckerboardBackgroundButton");
    private Button FilteringButtonControl =>
        GetRequiredButton("FilteringButton");
    private Button ModeMenuButtonControl =>
        this.FindControl<Button>("ModeSubmenuButton")
        ?? throw new InvalidOperationException(
            "The tool menu control is missing its mode menu button.");
    private Border ModeMenuControl =>
        this.FindControl<Border>("ModeMenuBorder")
        ?? throw new InvalidOperationException(
            "The tool menu control is missing its mode submenu.");
    private PathIcon ModeSubmenuIconControl =>
        this.FindControl<PathIcon>("ModeSubmenuIcon")
        ?? throw new InvalidOperationException(
            "The tool menu control is missing its submenu icon.");
    private Button MainModeButtonControl =>
        GetRequiredButton("MainModeButton");
    private Button ChannelModeButtonControl =>
        GetRequiredButton("ChannelModeButton");

    internal ImageViewerToolMenuControl(
        ImageViewerToolMenuViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        ModeSubmenuIconControl.Data = ViewerIconGeometries.Submenu;
    }

    private Button GetRequiredButton(string name)
    {
        return this.FindControl<Button>(name)
            ?? throw new InvalidOperationException(
                $"The tool menu control is missing its '{name}' button.");
    }
}

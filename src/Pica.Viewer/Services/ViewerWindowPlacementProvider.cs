namespace Pica.Viewer.Services;

internal sealed class ViewerWindowPlacementProvider :
    IViewerWindowPlacementProvider
{
    private ViewerWindowPlacement _currentPlacement;

    internal ViewerWindowPlacementProvider(
        ViewerWindowPlacement initialPlacement)
    {
        _currentPlacement = initialPlacement
            ?? throw new ArgumentNullException(nameof(initialPlacement));
    }

    public ViewerWindowPlacement GetCurrentPlacement()
    {
        return _currentPlacement;
    }

    internal void Update(ViewerWindowPlacement placement)
    {
        _currentPlacement = placement
            ?? throw new ArgumentNullException(nameof(placement));
    }
}

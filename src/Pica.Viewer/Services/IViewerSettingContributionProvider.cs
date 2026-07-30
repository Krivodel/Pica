namespace Pica.Viewer.Services;

public interface IViewerSettingContributionProvider
{
    Task<IReadOnlyList<ViewerSettingContribution>> CreateAsync(
        CancellationToken ct);
}

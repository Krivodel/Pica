namespace Pica.Viewer.Services;

internal sealed class CrossPlatformFileActions : PlatformFileActions
{
    public override bool SupportsOpenWith => false;

    private readonly IFileRevealPlatform _fileRevealPlatform;

    public CrossPlatformFileActions(IFileRevealPlatform fileRevealPlatform)
    {
        _fileRevealPlatform = fileRevealPlatform
            ?? throw new ArgumentNullException(nameof(fileRevealPlatform));
    }

    protected override IReadOnlyList<OpenWithApplication> GetOpenWithApplicationsCore(
        string filePath)
    {
        return new List<OpenWithApplication>();
    }

    protected override Task RevealInFolderCoreAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        return _fileRevealPlatform.RevealAsync(filePath, windowMode, ct);
    }

    protected override Task OpenWithCoreAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    protected override Task ChooseApplicationCoreAsync(
        string filePath,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

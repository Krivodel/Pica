namespace Pica.Viewer.Services.FileReveal;

internal interface IFileRevealProcessLauncher
{
    void Start(string executablePath, IReadOnlyList<string> arguments);
}

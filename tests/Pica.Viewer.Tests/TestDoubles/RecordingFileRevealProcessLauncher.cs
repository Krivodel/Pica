using Pica.Viewer.Services.FileReveal;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingFileRevealProcessLauncher
    : IFileRevealProcessLauncher
{
    public string? ExecutablePath { get; private set; }
    public IReadOnlyList<string> Arguments { get; private set; } = [];
    public int CallCount { get; private set; }

    public void Start(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        CallCount++;
        ExecutablePath = executablePath;
        Arguments = arguments.ToList();
    }
}

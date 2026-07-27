using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingFileRevealPlatform : IFileRevealPlatform
{
    public string? FilePath { get; private set; }
    public FileRevealWindowMode? WindowMode { get; private set; }
    public int CallCount { get; private set; }

    public Task RevealAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        CallCount++;
        FilePath = filePath;
        WindowMode = windowMode;

        return Task.CompletedTask;
    }
}

using Pica.Viewer.Services.FileReveal;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingStandardFileRevealer
    : IStandardFileRevealer
{
    public string? FilePath { get; private set; }
    public int CallCount { get; private set; }

    public void Reveal(string filePath)
    {
        CallCount++;
        FilePath = filePath;
    }
}

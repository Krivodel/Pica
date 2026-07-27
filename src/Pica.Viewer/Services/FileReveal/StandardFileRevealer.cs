namespace Pica.Viewer.Services.FileReveal;

internal sealed class StandardFileRevealer : IStandardFileRevealer
{
    public void Reveal(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (OperatingSystem.IsWindows())
        {
            WindowsFileReveal.Reveal(filePath);
            return;
        }

        CrossPlatformFileReveal.Reveal(filePath);
    }
}

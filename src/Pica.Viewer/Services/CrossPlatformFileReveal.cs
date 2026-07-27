using System.Diagnostics;

namespace Pica.Viewer.Services;

public static class CrossPlatformFileReveal
{
    public static void Reveal(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ProcessStartInfo startInfo = OperatingSystem.IsMacOS()
            ? CreateMacRevealStartInfo(filePath)
            : CreateLinuxRevealStartInfo(filePath);
        using Process? process = Process.Start(startInfo);
    }

    private static ProcessStartInfo CreateMacRevealStartInfo(string filePath)
    {
        ProcessStartInfo startInfo = new("/usr/bin/open")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-R");
        startInfo.ArgumentList.Add(filePath);

        return startInfo;
    }

    private static ProcessStartInfo CreateLinuxRevealStartInfo(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                "The image directory could not be determined.");
        ProcessStartInfo startInfo = new("xdg-open")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(directoryPath);

        return startInfo;
    }
}

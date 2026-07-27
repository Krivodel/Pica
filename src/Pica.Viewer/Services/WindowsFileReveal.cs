using System.Diagnostics;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services;

[SupportedOSPlatform("windows")]
public static class WindowsFileReveal
{
    private const string ExplorerFileName = "explorer.exe";
    private const string ExplorerSelectArgument = "/select,";

    public static void Reveal(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ProcessStartInfo startInfo = new(ExplorerFileName)
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(ExplorerSelectArgument);
        startInfo.ArgumentList.Add(filePath);
        using Process? process = Process.Start(startInfo);
    }
}

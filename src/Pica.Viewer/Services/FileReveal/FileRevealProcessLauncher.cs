using System.ComponentModel;
using System.Diagnostics;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class FileRevealProcessLauncher : IFileRevealProcessLauncher
{
    public void Start(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process? process = Process.Start(startInfo);

        if (process is null)
        {
            throw new Win32Exception(
                $"The file reveal process '{executablePath}' could not be started.");
        }
    }
}

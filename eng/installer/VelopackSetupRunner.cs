using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Pica.Installer;

internal sealed class VelopackSetupRunner
{
    private const int ElevationCancelledErrorCode = 1223;

    public void Run(string setupPath, InstallerDestination destination, InstallerDirectoryConsent consent)
    {
        if (string.IsNullOrWhiteSpace(setupPath))
        {
            throw new ArgumentException(
                "Setup path cannot be empty.",
                nameof(setupPath));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.ValidateForInstall(consent);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = $"--silent --installto \"{destination.Path}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using Process? process = Process.Start(startInfo);

            if (process is null)
            {
                throw new InvalidOperationException(
                    "Velopack setup process was not created.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Velopack setup exited with code {process.ExitCode}.");
            }
        }
        catch (Win32Exception ex)
            when (ex.NativeErrorCode == ElevationCancelledErrorCode)
        {
            throw new OperationCanceledException(
                "Windows elevation was cancelled.",
                ex);
        }
    }
}

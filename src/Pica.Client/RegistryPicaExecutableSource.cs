using Microsoft.Win32;

using Pica.Protocol;

namespace Pica.Client;

public sealed class RegistryPicaExecutableSource : IPicaExecutableSource
{
    private static readonly string AppPathKey =
        $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{PicaProtocolConstants.ExecutableName}";

    public IEnumerable<string> GetCandidatePaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? appPathKey = baseKey.OpenSubKey(AppPathKey);
                string? executablePath = appPathKey?.GetValue(null) as string;

                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    yield return executablePath;
                }
            }
        }
    }
}

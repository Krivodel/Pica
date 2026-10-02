namespace Pica.Desktop.Services;

internal static class PicaInstallation
{
    internal static string ExecutablePath => ResolveExecutablePath(Environment.ProcessPath
        ?? throw new InvalidOperationException("Pica executable path is unavailable."));
    internal static bool HasFirstRunMarker => File.Exists(GetFirstRunMarkerPath(ExecutablePath));

    private const string FirstRunMarkerFileName = ".pica-file-associations-prompt";

    internal static string ResolveExecutablePath(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        DirectoryInfo? directory = Directory.GetParent(executable);
        string? root = directory?.Parent?.FullName;
        string? stablePath = root is null ? null : Path.Combine(root, Path.GetFileName(executable));

        return (directory?.Name == "current") && (stablePath is not null) && File.Exists(stablePath)
            ? stablePath : executable;
    }

    internal static void RecordFirstRun(string executablePath)
    {
        // Silent installs do not launch Pica; preserve the signal outside the replaceable current directory.
        File.WriteAllText(GetFirstRunMarkerPath(executablePath), string.Empty);
    }

    internal static string GetFirstRunMarkerPath(string executablePath)
    {
        string directory = Path.GetDirectoryName(ResolveExecutablePath(executablePath))
            ?? throw new InvalidOperationException("The Pica installation directory is unavailable.");

        return Path.Combine(directory, FirstRunMarkerFileName);
    }
}

namespace Pica.Desktop.Services;

internal static class PicaInstallation
{
    internal static string ExecutablePath => ResolveExecutablePath(Environment.ProcessPath
        ?? throw new InvalidOperationException("Pica executable path is unavailable."));

    internal static string ResolveExecutablePath(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        DirectoryInfo? directory = Directory.GetParent(executable);
        string? root = directory?.Parent?.FullName;
        string? stablePath = root is null ? null : Path.Combine(root, Path.GetFileName(executable));

        return (directory?.Name == "current") && (stablePath is not null) && File.Exists(stablePath)
            ? stablePath : executable;
    }
}

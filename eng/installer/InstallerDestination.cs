using System.Collections.Generic;
using System.IO;

namespace Pica.Installer;

internal sealed class InstallerDestination
{
    public string Path { get; }
    public bool HasContents { get; }

    private InstallerDestination(string path, bool hasContents)
    {
        Path = path;
        HasContents = hasContents;
    }

    public static InstallerDestination Inspect(string path)
    {
        string normalizedPath = InstallerPathValidator.NormalizeAndValidate(path);
        bool hasContents = false;

        if (Directory.Exists(normalizedPath))
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(normalizedPath).GetEnumerator();
            hasContents = entries.MoveNext();
        }

        return new InstallerDestination(normalizedPath, hasContents);
    }

    public void ValidateForInstall(InstallerDirectoryConsent consent)
    {
        InstallerDestination current = Inspect(Path);

        if (current.HasContents && (consent != InstallerDirectoryConsent.ReplaceExistingContents))
        {
            throw new IOException("The installation directory became nonempty without replacement consent.");
        }
    }
}

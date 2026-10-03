using System;
using System.IO;
using System.Reflection;
using System.Security;

namespace Pica.Installer;

internal static class InstallerPathValidator
{
    public static string GetDefaultInstallPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            InstallerProduct.ApplicationDirectoryName);
    }

    public static string NormalizeAndValidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Installation path cannot be empty.",
                nameof(path));
        }

        string trimmedPath = path.Trim();
        string? requestedRoot = Path.GetPathRoot(trimmedPath);

        if (!Path.IsPathRooted(trimmedPath)
            || (requestedRoot?.Length <= 2))
        {
            throw new ArgumentException(
                "Installation path must be absolute.",
                nameof(path));
        }

        string normalizedPath = TrimTrailingSeparators(
            NormalizeFullPath(trimmedPath, nameof(path)));
        string? rootPath = Path.GetPathRoot(normalizedPath);

        if (string.IsNullOrWhiteSpace(rootPath)
            || string.Equals(
                normalizedPath,
                TrimTrailingSeparators(rootPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Installation path cannot be a drive root.",
                nameof(path));
        }

        string dataPath = TrimTrailingSeparators(NormalizeFullPath(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                InstallerProduct.DataDirectoryName),
            nameof(path)));

        if (PathsOverlap(normalizedPath, dataPath))
        {
            throw new ArgumentException(
                "Installation path must not overlap the Pica data directory.",
                nameof(path));
        }

        Environment.SpecialFolder[] protectedFolders =
        {
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData
        };

        foreach (Environment.SpecialFolder folder in protectedFolders)
        {
            string protectedPath = Environment.GetFolderPath(folder);

            if (string.IsNullOrWhiteSpace(protectedPath))
            {
                continue;
            }

            string normalizedProtectedPath = TrimTrailingSeparators(NormalizeFullPath(protectedPath, nameof(path)));

            if (IsSamePathOrDescendant(normalizedProtectedPath, normalizedPath)
                || ((folder == Environment.SpecialFolder.Windows)
                    && IsSamePathOrDescendant(normalizedPath, normalizedProtectedPath)))
            {
                throw new ArgumentException("Installation path cannot contain a shared system directory.", nameof(path));
            }
        }

        string installerPath = Assembly.GetExecutingAssembly().Location;

        if (!string.IsNullOrWhiteSpace(installerPath) && IsSamePathOrDescendant(installerPath, normalizedPath))
        {
            throw new ArgumentException("Installation path cannot contain the running installer.", nameof(path));
        }

        ValidateDirectoryAncestors(normalizedPath);

        return normalizedPath;
    }

    private static void ValidateDirectoryAncestors(string path)
    {
        DirectoryInfo? directory = new DirectoryInfo(path);

        while (directory is not null)
        {
            FileAttributes attributes;

            try
            {
                attributes = File.GetAttributes(directory.FullName);
            }
            catch (FileNotFoundException)
            {
                directory = directory.Parent;
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                directory = directory.Parent;
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Installation path must not traverse a directory link.", nameof(path));
            }

            if ((attributes & FileAttributes.Directory) == 0)
            {
                throw new ArgumentException("Installation path must identify a directory.", nameof(path));
            }

            directory = directory.Parent;
        }
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        return IsSamePathOrDescendant(firstPath, secondPath)
            || IsSamePathOrDescendant(secondPath, firstPath);
    }

    private static string NormalizeFullPath(
        string path,
        string parameterName)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (NotSupportedException ex)
        {
            throw new ArgumentException(
                "Installation path uses an unsupported format.",
                parameterName,
                ex);
        }
        catch (PathTooLongException ex)
        {
            throw new ArgumentException(
                "Installation path is too long.",
                parameterName,
                ex);
        }
        catch (SecurityException ex)
        {
            throw new ArgumentException(
                "Installation path cannot be accessed.",
                parameterName,
                ex);
        }
    }

    private static bool IsSamePathOrDescendant(
        string candidatePath,
        string rootPath)
    {
        if (string.Equals(
            candidatePath,
            rootPath,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string rootPathWithSeparator = rootPath
            + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(
            rootPathWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingSeparators(string path)
    {
        return path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }
}

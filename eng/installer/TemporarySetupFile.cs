using System;
using System.IO;
using System.Reflection;

namespace Pica.Installer;

internal sealed class TemporarySetupFile : IDisposable
{
    public string Path { get; }

    private TemporarySetupFile(string path)
    {
        Path = path;
    }

    public static TemporarySetupFile Create()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{InstallerProduct.ApplicationName}-Velopack-{Guid.NewGuid():N}.exe");
        Assembly assembly = typeof(TemporarySetupFile).Assembly;
        Stream? resourceStream = assembly.GetManifestResourceStream(
            InstallerProduct.VelopackSetupResourceName);

        if (resourceStream is null)
        {
            throw new InvalidOperationException(
                "Embedded Velopack setup resource was not found.");
        }

        try
        {
            using FileStream outputStream = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            resourceStream.CopyTo(outputStream);
            outputStream.Flush(true);
        }
        catch (IOException)
        {
            DeleteIfExists(path);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            DeleteIfExists(path);
            throw;
        }
        finally
        {
            resourceStream.Dispose();
        }

        return new TemporarySetupFile(path);
    }

    public void Dispose()
    {
        DeleteIfExists(Path);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

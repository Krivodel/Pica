namespace Pica.Tests.Common;

public sealed class PicaTemporaryDirectory : IDisposable
{
    public string DirectoryPath { get; }

    private const string RootDirectoryName = "Pica.Tests";

    private bool _disposed;

    public PicaTemporaryDirectory()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            RootDirectoryName,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}

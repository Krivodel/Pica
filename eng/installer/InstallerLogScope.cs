using System;

namespace Pica.Installer;

internal sealed class InstallerLogScope : IDisposable
{
    public static InstallerLogScope Instance { get; } =
        new InstallerLogScope();

    private InstallerLogScope()
    {
    }

    public void Dispose()
    {
    }
}

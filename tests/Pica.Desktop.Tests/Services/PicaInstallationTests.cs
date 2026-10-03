using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaInstallationTests
{
    [Fact]
    public void ResolveExecutablePath_VersionDirectoryWithLauncher_UsesStableExecutable()
    {
        using PicaTemporaryDirectory directory = new();
        string stableExecutable = Path.Combine(directory.DirectoryPath, "Pica.exe");
        string currentDirectory = Path.Combine(directory.DirectoryPath, "current");
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(stableExecutable, "Stable launcher");
        string executable = Path.Combine(currentDirectory, "Pica.exe");

        string resolved = PicaInstallation.ResolveExecutablePath(executable);

        resolved.Should().Be(stableExecutable);
    }

    [Fact]
    public void ResolveExecutablePath_StandaloneBinaryWithoutLauncher_ReturnsOriginalExecutable()
    {
        using PicaTemporaryDirectory directory = new();
        string executable = Path.Combine(directory.DirectoryPath, "Pica.exe");

        string resolved = PicaInstallation.ResolveExecutablePath(executable);

        resolved.Should().Be(executable);
    }
}

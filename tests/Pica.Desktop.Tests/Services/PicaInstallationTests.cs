using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaInstallationTests
{
    [Fact]
    public void RecordFirstRun_SilentInstall_StoresMarkerOutsideVersionDirectory()
    {
        using PicaTemporaryDirectory directory = new();
        string stableExecutable = Path.Combine(directory.DirectoryPath, "Pica.exe");
        string currentDirectory = Path.Combine(directory.DirectoryPath, "current");
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(stableExecutable, "Stable launcher");
        string executable = Path.Combine(currentDirectory, "Pica.exe");

        PicaInstallation.RecordFirstRun(executable);
        string marker = PicaInstallation.GetFirstRunMarkerPath(executable);
        Directory.Delete(currentDirectory);
        Directory.CreateDirectory(currentDirectory);

        File.Exists(marker).Should().BeTrue();
        Path.GetDirectoryName(marker).Should().Be(directory.DirectoryPath);
        PicaInstallation.ResolveExecutablePath(executable).Should().Be(stableExecutable);
    }

    [Fact]
    public void ResolveExecutablePath_StandaloneBinaryWithoutLauncher_ReturnsOriginalExecutable()
    {
        using PicaTemporaryDirectory directory = new();
        string executable = Path.Combine(directory.DirectoryPath, "Pica.exe");

        string resolved = PicaInstallation.ResolveExecutablePath(executable);

        resolved.Should().Be(executable);
        File.Exists(PicaInstallation.GetFirstRunMarkerPath(executable)).Should().BeFalse();
    }
}

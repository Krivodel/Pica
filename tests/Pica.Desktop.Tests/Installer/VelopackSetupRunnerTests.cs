using FluentAssertions;
using Xunit;

using Pica.Installer;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Installer;

public sealed class VelopackSetupRunnerTests
{
    [Fact]
    public void Run_DestinationBecameNonempty_RejectsBeforeLaunchingInstaller()
    {
        using PicaTemporaryDirectory directory = new();
        string destinationPath = Path.Combine(directory.DirectoryPath, "Pica");
        InstallerDestination destination = InstallerDestination.Inspect(destinationPath);
        Directory.CreateDirectory(destinationPath);
        string filePath = Path.Combine(destinationPath, "existing.txt");
        File.WriteAllText(filePath, "Existing data");
        VelopackSetupRunner runner = new();
        string missingSetupPath = Path.Combine(directory.DirectoryPath, "missing-setup.exe");

        Action install = () => runner.Run(missingSetupPath, destination, InstallerDirectoryConsent.EmptyDirectoryOnly);

        install.Should().Throw<IOException>();
        File.ReadAllText(filePath).Should().Be("Existing data");
    }
}

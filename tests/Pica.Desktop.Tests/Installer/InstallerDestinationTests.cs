using FluentAssertions;
using Xunit;

using Pica.Installer;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Installer;

public sealed class InstallerDestinationTests
{
    [Fact]
    public void Inspect_MissingDirectory_DoesNotCreateDirectory()
    {
        using PicaTemporaryDirectory directory = new();
        string path = Path.Combine(directory.DirectoryPath, "Pica");

        InstallerDestination destination = InstallerDestination.Inspect(path);

        destination.HasContents.Should().BeFalse();
        destination.Path.Should().Be(path);
        Directory.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Inspect_EmptyDirectory_DoesNotRequireReplacement()
    {
        using PicaTemporaryDirectory directory = new();

        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        destination.HasContents.Should().BeFalse();
    }

    [Fact]
    public void Inspect_HiddenFile_RequiresReplacement()
    {
        using PicaTemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.DirectoryPath, "hidden.txt");
        File.WriteAllText(filePath, "Existing data");
        File.SetAttributes(filePath, FileAttributes.Hidden);

        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        destination.HasContents.Should().BeTrue();
        File.ReadAllText(filePath).Should().Be("Existing data");
    }

    [Fact]
    public void Inspect_EmptySubdirectory_RequiresReplacement()
    {
        using PicaTemporaryDirectory directory = new();
        Directory.CreateDirectory(Path.Combine(directory.DirectoryPath, "empty"));

        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        destination.HasContents.Should().BeTrue();
    }

    [Fact]
    public void ValidateForInstall_ReplacementNotConfirmed_PreservesExistingFiles()
    {
        using PicaTemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.DirectoryPath, "existing.txt");
        File.WriteAllText(filePath, "Existing data");
        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        Action validate = () => destination.ValidateForInstall(InstallerDirectoryConsent.EmptyDirectoryOnly);

        validate.Should().Throw<IOException>();
        File.ReadAllText(filePath).Should().Be("Existing data");
    }

    [Fact]
    public void ValidateForInstall_ReplacementConfirmed_LeavesCleanupToVelopack()
    {
        using PicaTemporaryDirectory directory = new();
        string currentPath = Path.Combine(directory.DirectoryPath, "current");
        Directory.CreateDirectory(currentPath);
        string filePath = Path.Combine(currentPath, "Pica.exe");
        File.WriteAllText(filePath, "Existing application");
        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        destination.ValidateForInstall(InstallerDirectoryConsent.ReplaceExistingContents);

        File.ReadAllText(filePath).Should().Be("Existing application");
    }

    [Fact]
    public void ValidateForInstall_FileCreatedAfterInspection_RejectsReplacementWithoutConsent()
    {
        using PicaTemporaryDirectory directory = new();
        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);
        string filePath = Path.Combine(directory.DirectoryPath, "new.txt");
        File.WriteAllText(filePath, "New data");

        Action validate = () => destination.ValidateForInstall(InstallerDirectoryConsent.EmptyDirectoryOnly);

        validate.Should().Throw<IOException>();
        File.ReadAllText(filePath).Should().Be("New data");
    }

    [Fact]
    public void ValidateForInstall_EmptyDirectory_AllowsInstallationWithoutReplacementConsent()
    {
        using PicaTemporaryDirectory directory = new();
        InstallerDestination destination = InstallerDestination.Inspect(directory.DirectoryPath);

        destination.ValidateForInstall(InstallerDirectoryConsent.EmptyDirectoryOnly);

        Directory.EnumerateFileSystemEntries(directory.DirectoryPath).Should().BeEmpty();
    }
}

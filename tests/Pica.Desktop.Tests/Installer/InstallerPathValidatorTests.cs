using System.Reflection;

using FluentAssertions;
using Xunit;

using Pica.Installer;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Installer;

public sealed class InstallerPathValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Pica")]
    [InlineData("C:Pica")]
    [InlineData("\\Pica")]
    [InlineData("C:\\")]
    public void NormalizeAndValidate_IncompletePathOrDriveRoot_RejectsPath(string path)
    {
        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(Environment.SpecialFolder.Windows)]
    [InlineData(Environment.SpecialFolder.System)]
    [InlineData(Environment.SpecialFolder.ProgramFiles)]
    [InlineData(Environment.SpecialFolder.ProgramFilesX86)]
    [InlineData(Environment.SpecialFolder.CommonApplicationData)]
    public void NormalizeAndValidate_SharedSystemDirectory_RejectsPath(Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder);

        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeAndValidate_DirectoryInsideWindows_RejectsPath()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Pica");

        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeAndValidate_DefaultApplicationDirectory_AllowsPath()
    {
        string path = InstallerPathValidator.GetDefaultInstallPath();

        string normalizedPath = InstallerPathValidator.NormalizeAndValidate(path);

        normalizedPath.Should().Be(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("child")]
    [InlineData("..")]
    public void NormalizeAndValidate_OverlappingPicaDataDirectory_RejectsPath(string relativePath)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallerProduct.DataDirectoryName,
            relativePath);

        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeAndValidate_DirectoryContainingInstaller_RejectsPath()
    {
        string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Test assembly directory is unavailable.");

        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeAndValidate_ExistingFile_RejectsPathAndPreservesFile()
    {
        using PicaTemporaryDirectory directory = new();
        string path = Path.Combine(directory.DirectoryPath, "Pica");
        File.WriteAllText(path, "Existing data");

        Action validate = () => InstallerPathValidator.NormalizeAndValidate(path);

        validate.Should().Throw<ArgumentException>();
        File.ReadAllText(path).Should().Be("Existing data");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pica")]
    public void NormalizeAndValidate_DirectoryLinkOrItsChild_RejectsPath(string relativePath)
    {
        using PicaTemporaryDirectory directory = new();
        string targetPath = Path.Combine(directory.DirectoryPath, "target");
        string linkPath = Path.Combine(directory.DirectoryPath, "link");
        Directory.CreateDirectory(targetPath);
        Directory.CreateSymbolicLink(linkPath, targetPath);

        try
        {
            Action validate = () => InstallerPathValidator.NormalizeAndValidate(Path.Combine(linkPath, relativePath));

            validate.Should().Throw<ArgumentException>();
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }
}

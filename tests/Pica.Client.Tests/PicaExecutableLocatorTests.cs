using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;

namespace Pica.Client.Tests;

public sealed class PicaExecutableLocatorTests
{
    [Fact]
    public void FindExecutablePath_WhenCandidateExists_ReturnsFullPath()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string executablePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            PicaProtocolConstants.ExecutableName);
        File.WriteAllBytes(executablePath, Array.Empty<byte>());
        MakeExecutableOnUnix(executablePath);

        string? result = FindExecutablePath(executablePath);

        result.Should().Be(Path.GetFullPath(executablePath));
    }

    [Fact]
    public void FindExecutablePath_WhenCandidatesDoNotExist_ReturnsNull()
    {
        string candidatePath = @"Z:\Missing\Pica.exe";

        string? result = FindExecutablePath(candidatePath);

        result.Should().BeNull();
    }

    private static string? FindExecutablePath(string candidatePath)
    {
        FixedPicaExecutableSource source = new(candidatePath);
        PicaExecutableLocator locator = new(
            new List<IPicaExecutableSource> { source },
            NullLogger<PicaExecutableLocator>.Instance);

        return locator.FindExecutablePath();
    }

    private static void MakeExecutableOnUnix(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            filePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}

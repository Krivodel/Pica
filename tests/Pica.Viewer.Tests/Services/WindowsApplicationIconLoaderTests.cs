using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsApplicationIconLoaderTests
{
    [Fact]
    public void Load_WithWindowsExecutable_ReturnsPngIcon()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string executablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "explorer.exe");

        WindowsApplicationIconLoader loader = new(
            NullLogger<WindowsApplicationIconLoader>.Instance);
        byte[]? icon = loader.Load(
            executablePath,
            0,
            executablePath);

        icon.Should().NotBeNullOrEmpty();
    }
}

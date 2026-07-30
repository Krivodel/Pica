using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaDesktopStateServiceTests
{
    [Fact]
    public async Task LoadAsync_WithoutSavedState_UsesSixtySecondTimeout()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        PicaDesktopStateService service = CreateService(
            temporaryDirectory);

        PicaDesktopState state = await service.LoadAsync(
            CancellationToken.None);

        state.BackgroundIdleTimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public async Task SaveAsync_WithBackgroundIdleTimeout_RoundTripsState()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        PicaDesktopStateService service = CreateService(
            temporaryDirectory);
        PicaDesktopState state = new()
        {
            BackgroundIdleTimeoutSeconds = 300
        };

        await service.SaveAsync(state, CancellationToken.None);
        PicaDesktopStateService reader = CreateService(
            temporaryDirectory);
        PicaDesktopState restoredState = await reader.LoadAsync(
            CancellationToken.None);

        restoredState.BackgroundIdleTimeoutSeconds.Should().Be(300);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(120)]
    [InlineData(3601)]
    public async Task LoadAsync_WithUnsupportedTimeout_UsesDefault(
        int timeoutSeconds)
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string stateFilePath = CreateStateFilePath(
            temporaryDirectory);
        string stateJson = $$"""
            {
              "backgroundIdleTimeoutSeconds": {{timeoutSeconds}}
            }
            """;
        await File.WriteAllTextAsync(
            stateFilePath,
            stateJson,
            CancellationToken.None);
        PicaDesktopStateService service = CreateService(
            temporaryDirectory);

        PicaDesktopState state = await service.LoadAsync(
            CancellationToken.None);

        state.BackgroundIdleTimeoutSeconds.Should().Be(60);
    }

    private static PicaDesktopStateService CreateService(
        PicaTemporaryDirectory temporaryDirectory)
    {
        return new PicaDesktopStateService(
            CreateStateFilePath(temporaryDirectory),
            NullLogger<PicaDesktopStateService>.Instance);
    }

    private static string CreateStateFilePath(
        PicaTemporaryDirectory temporaryDirectory)
    {
        return Path.Combine(
            temporaryDirectory.DirectoryPath,
            "desktop.json");
    }
}

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

    [Fact]
    public async Task UpdateAsync_FromIndependentReaders_PreservesOtherSettingsAndReloadsChanges()
    {
        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService first = CreateService(directory);
        PicaDesktopStateService second = CreateService(directory);
        await first.LoadAsync(CancellationToken.None);
        PicaClipboardShortcutGesture gesture = new(0x56, PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt);

        await Task.WhenAll(
            first.UpdateAsync(state => state.ClipboardShortcut = gesture, CancellationToken.None),
            second.UpdateAsync(state => state.BackgroundIdleTimeoutSeconds = 300, CancellationToken.None));
        PicaDesktopState actual = await first.LoadAsync(CancellationToken.None);

        actual.ClipboardShortcut.Should().Be(gesture);
        actual.BackgroundIdleTimeoutSeconds.Should().Be(300);
        actual.IsClipboardShortcutEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenCanceledDuringWrite_KeepsPreviouslySavedState()
    {
        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService service = CreateService(directory);
        await service.UpdateAsync(state => state.BackgroundIdleTimeoutSeconds = 300, CancellationToken.None);
        using CancellationTokenSource cancellation = new();

        Func<Task> change = () => service.UpdateAsync(state =>
        {
            state.BackgroundIdleTimeoutSeconds = 0;
            cancellation.Cancel();
        }, cancellation.Token);

        await change.Should().ThrowAsync<OperationCanceledException>();
        (await service.LoadAsync(CancellationToken.None)).BackgroundIdleTimeoutSeconds.Should().Be(300);
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

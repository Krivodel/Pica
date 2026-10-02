using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Win32.Input;
using FluentAssertions;
using Velopack.Windows;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Tests.Services.Background;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services;

[Collection(WindowsShortcutCollection.Name)]
public sealed class PicaClipboardShortcutRegistrationTests
{
    [Fact]
    public async Task ApplyAsync_InExplorerMode_RecordsAndChangesShortcutWithoutStartingAgent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService stateService = CreateStateService(directory);
        string startupPath = Path.Combine(directory.DirectoryPath, "clipboard.lnk");
        string explorerPath = startupPath + ".explorer.lnk";
        PicaClipboardShortcutRegistration registration = CreateRegistration(stateService, startupPath);
        string pipeName = "Pica.Tests.Explorer." + Guid.NewGuid().ToString("N");
        PicaClipboardShortcutService client = new(pipeName,
            () => throw new InvalidOperationException("Explorer mode must not start an agent."), stateService, registration);
        PicaClipboardShortcutGesture first = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture second = CreateGesture(Key.F24);
        await using WindowsClipboardHotKey probe = new(() => { });

        await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, first), CancellationToken.None);
        File.Exists(explorerPath).Should().BeFalse();
        await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true), CancellationToken.None);
        await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, second), CancellationToken.None);
        await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, second), CancellationToken.None);

        using (ShellLink link = new(explorerPath))
        {
            link.Target.Should().Be(Environment.ProcessPath);
            link.Arguments.Should().Be(PicaLaunchArguments.ClipboardArgument);
            link.HotKey.Should().Be(second.ShellHotKey);
        }

        File.Exists(startupPath).Should().BeFalse();
        client.CurrentState.RequiresClipboardAgent.Should().BeFalse();
        await probe.ValidateAsync(first, CancellationToken.None);
        await probe.ValidateAsync(second, CancellationToken.None);
        await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: false), CancellationToken.None);
        File.Exists(explorerPath).Should().BeFalse();
        (await stateService.LoadAsync(CancellationToken.None)).ClipboardShortcut.Should().Be(second);
    }

    [Fact]
    public async Task ApplyAsync_WhenSwitchingModes_StartsAgentOnlyWhenRequiredAndStopsItImmediately()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService stateService = CreateStateService(directory);
        string startupPath = Path.Combine(directory.DirectoryPath, "clipboard.lnk");
        string explorerPath = startupPath + ".explorer.lnk";
        string pipeName = "Pica.Tests.Modes." + Guid.NewGuid().ToString("N");
        PicaClipboardAgent agent = new(stateService, NullLogger<PicaClipboardAgent>.Instance,
            startupPath, Environment.ProcessPath ?? string.Empty, _ => Task.CompletedTask, pipeName,
            clearWindowHotKeys: () => { });
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        Task serving = Task.CompletedTask;
        int starts = 0;
        PicaClipboardShortcutService client = new(pipeName, () =>
        {
            starts++;
            serving = agent.RunAsync(timeout.Token);
        }, stateService, CreateRegistration(stateService, startupPath));
        PicaClipboardShortcutGesture first = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture second = CreateGesture(Key.F24);
        await using WindowsClipboardHotKey probe = new(() => { });

        try
        {
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, first), timeout.Token);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true), timeout.Token);
            starts.Should().Be(0);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled, IsEnabled: true), timeout.Token);
            starts.Should().Be(1);
            File.Exists(startupPath).Should().BeTrue();
            File.Exists(explorerPath).Should().BeFalse();
            Func<Task> occupied = () => probe.ValidateAsync(first, timeout.Token);
            (await occupied.Should().ThrowAsync<PicaShortcutException>()).Which.Failure.Should().Be(PicaShortcutFailure.Occupied);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, second), timeout.Token);
            await probe.ValidateAsync(first, timeout.Token);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled, IsEnabled: false), timeout.Token);
            await serving.WaitAsync(timeout.Token);

            starts.Should().Be(1);
            File.Exists(startupPath).Should().BeFalse();
            File.Exists(explorerPath).Should().BeTrue();
            await probe.ValidateAsync(second, timeout.Token);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: false), timeout.Token);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled, IsEnabled: true), timeout.Token);
            starts.Should().Be(1);
            client.CurrentState.RequiresClipboardAgent.Should().BeFalse();
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true), timeout.Token);
            starts.Should().Be(2);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: false), timeout.Token);
            await serving.WaitAsync(timeout.Token);
            File.Exists(startupPath).Should().BeFalse();
            File.Exists(explorerPath).Should().BeFalse();
            await probe.ValidateAsync(second, timeout.Token);
        }
        finally
        {
            timeout.Cancel();
            await serving;
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenModeSaveFails_RestoresResidentRegistrationAndBothShortcuts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaClipboardShortcutGesture gesture = CreateGesture(Key.F23);
        FailingDesktopStateService stateService = new(new PicaDesktopState
        {
            IsClipboardShortcutEnabled = true,
            IsFullscreenClipboardShortcutEnabled = true,
            ClipboardShortcut = gesture
        });
        string startupPath = Path.Combine(directory.DirectoryPath, "clipboard.lnk");
        using (ShellLink link = new() { Target = Environment.ProcessPath, Arguments = PicaLaunchArguments.ClipboardAgentArgument })
        {
            link.Save(startupPath);
        }

        byte[] previous = await File.ReadAllBytesAsync(startupPath);
        PicaClipboardShortcutRegistration registration = CreateRegistration(stateService, startupPath);
        await using WindowsClipboardHotKey owner = new(() => { });
        await owner.SetAsync(gesture, CancellationToken.None);

        Func<Task> change = () => registration.ApplyAsync(
            new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled, IsEnabled: false),
            owner, true, CancellationToken.None);

        await change.Should().ThrowAsync<IOException>();
        (await File.ReadAllBytesAsync(startupPath)).Should().Equal(previous);
        File.Exists(startupPath + ".explorer.lnk").Should().BeFalse();
        (await stateService.LoadAsync(CancellationToken.None)).RequiresClipboardAgent.Should().BeTrue();
        await owner.ValidateAsync(gesture, CancellationToken.None);
    }

    private static PicaDesktopStateService CreateStateService(PicaTemporaryDirectory directory)
    {
        return new PicaDesktopStateService(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
    }

    private static PicaClipboardShortcutRegistration CreateRegistration(IPicaDesktopStateService stateService, string startupPath)
    {
        return new PicaClipboardShortcutRegistration(stateService, NullLogger.Instance, startupPath,
            startupPath + ".explorer.lnk", Environment.ProcessPath ?? string.Empty, _ => Task.CompletedTask, () => { });
    }

    private static PicaClipboardShortcutGesture CreateGesture(Key key)
    {
        return new PicaClipboardShortcutGesture(KeyInterop.VirtualKeyFromKey(key),
            PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift);
    }
}

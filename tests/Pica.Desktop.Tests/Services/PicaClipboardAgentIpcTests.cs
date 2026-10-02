using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Win32.Input;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Tests.Services.Background;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services;

[Collection(WindowsShortcutCollection.Name)]
public sealed class PicaClipboardAgentIpcTests
{
    [Fact]
    public async Task ApplyAsync_WithIsolatedAgent_ReportsOccupiedAndRetainsWorkingGesture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
        string pipeName = "Pica.Tests.Agent." + Guid.NewGuid().ToString("N");
        PicaClipboardShortcutGesture first = new(KeyInterop.VirtualKeyFromKey(Key.F23), PicaShortcutModifiers.None);
        await stateService.UpdateAsync(state =>
        {
            state.IsClipboardShortcutEnabled = true;
            state.IsFullscreenClipboardShortcutEnabled = true;
            state.ClipboardShortcut = first;
        }, CancellationToken.None);
        PicaClipboardAgent agent = new(stateService, NullLogger<PicaClipboardAgent>.Instance,
            Path.Combine(directory.DirectoryPath, "clipboard.lnk"), Environment.ProcessPath ?? string.Empty,
            _ => Task.CompletedTask, pipeName, clearWindowHotKeys: () => { });
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        Task serving = agent.RunAsync(cancellation.Token);
        PicaClipboardShortcutService client = new(pipeName, () => throw new InvalidOperationException("The test agent must be running."));
        PicaClipboardShortcutGesture occupied = new(KeyInterop.VirtualKeyFromKey(Key.F24), PicaShortcutModifiers.None);
        await using WindowsClipboardHotKey other = new(() => { });
        await other.SetAsync(occupied, cancellation.Token);

        try
        {
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, first), cancellation.Token);
            await client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true), cancellation.Token);
            Func<Task> change = () => client.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, occupied), cancellation.Token);

            (await change.Should().ThrowAsync<PicaShortcutException>()).Which.Failure.Should().Be(PicaShortcutFailure.Occupied);
            client.CurrentState.ClipboardShortcut.Should().Be(first);
            client.CurrentState.IsClipboardShortcutEnabled.Should().BeTrue();
            PicaClipboardShortcutSettingContributionProvider.GetErrorMessage(new PicaShortcutException(PicaShortcutFailure.Occupied))
                .Should().Contain("занято");
        }
        finally
        {
            cancellation.Cancel();
            await serving;
        }
    }

    [Fact]
    public async Task RunAsync_WithForegroundFullscreenWindow_ReceivesRepeatedSystemHotKeyAndSuppressesRecording()
    {
        if (!OperatingSystem.IsWindows() || (Environment.GetEnvironmentVariable("PICA_TEST_FULLSCREEN_HOTKEY") != "1"))
        {
            return;
        }

        await using WindowsShortcutTestWindow window = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        nint handle = await window.Handle.WaitAsync(timeout.Token);
        int activationCount = 0;
        TaskCompletionSource activated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PicaClipboardShortcutGesture gesture = new(KeyInterop.VirtualKeyFromKey(Key.F24), PicaShortcutModifiers.None);
        await using WindowsClipboardHotKey hotKey = new(() =>
        {
            WindowsShortcutTestWindow.Activate(handle);
            Interlocked.Increment(ref activationCount);
            activated.TrySetResult();
        });
        await hotKey.SetAsync(gesture, timeout.Token);

        WindowsShortcutTestWindow.PressKey(gesture.VirtualKey);
        await activated.Task.WaitAsync(timeout.Token);
        WindowsShortcutNative.GetForegroundWindow().Should().Be(handle);
        activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WindowsShortcutTestWindow.PressKey(gesture.VirtualKey);
        await activated.Task.WaitAsync(timeout.Token);
        WindowsShortcutRecorder recorder = new(NullLogger<WindowsShortcutRecorder>.Instance);
        Task<PicaClipboardShortcutGesture> recording = recorder.RecordAsync(handle, timeout.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        WindowsShortcutTestWindow.PressKey(gesture.VirtualKey);
        PicaClipboardShortcutGesture recorded = await recording.WaitAsync(timeout.Token);

        recorded.Should().Be(gesture);
        activationCount.Should().Be(2);
        await hotKey.ValidateAsync(recorded, timeout.Token);
        activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WindowsShortcutTestWindow.PressKey(gesture.VirtualKey);
        await activated.Task.WaitAsync(timeout.Token);
        activationCount.Should().Be(3);
    }
}

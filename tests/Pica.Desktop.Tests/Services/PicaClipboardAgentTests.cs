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
public sealed class PicaClipboardAgentTests
{
    [Fact]
    public async Task ApplyAsync_WithRecordingWhileDisabledAndRepeatedChanges_KeepsOneNativeRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
        string shortcutPath = Path.Combine(directory.DirectoryPath, "clipboard.lnk");
        PicaClipboardAgent agent = CreateAgent(stateService, shortcutPath);
        PicaClipboardShortcutGesture first = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture second = CreateGesture(Key.F24);
        await using WindowsClipboardHotKey owner = new(() => { });
        await using WindowsClipboardHotKey probe = new(() => { });

        await agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, first), owner, CancellationToken.None);
        PicaDesktopState disabled = await stateService.LoadAsync(CancellationToken.None);
        disabled.IsClipboardShortcutEnabled.Should().BeFalse();
        disabled.ClipboardShortcut.Should().Be(first);
        File.Exists(shortcutPath).Should().BeFalse();
        await probe.ValidateAsync(first, CancellationToken.None);
        await agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetFullscreenEnabled, IsEnabled: true), owner, CancellationToken.None);
        await agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true), owner, CancellationToken.None);
        await agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, second), owner, CancellationToken.None);
        await probe.ValidateAsync(first, CancellationToken.None);
        await agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, first), owner, CancellationToken.None);
        await probe.ValidateAsync(second, CancellationToken.None);
        await owner.ValidateAsync(first, CancellationToken.None);

        using ShellLink link = new(shortcutPath);
        link.HotKey.Should().Be(0);
        link.Arguments.Should().Be(PicaLaunchArguments.ClipboardAgentArgument);
        (await stateService.LoadAsync(CancellationToken.None)).ClipboardShortcut.Should().Be(first);
    }

    [Fact]
    public async Task ApplyAsync_WhenStateWriteFails_RestoresPreviousRegistrationAndStartupShortcut()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaClipboardShortcutGesture previous = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture candidate = CreateGesture(Key.F24);
        FailingDesktopStateService stateService = new(new PicaDesktopState
        {
            IsClipboardShortcutEnabled = true,
            IsFullscreenClipboardShortcutEnabled = true,
            ClipboardShortcut = previous
        });
        string path = Path.Combine(directory.DirectoryPath, "clipboard.lnk");
        using (ShellLink shortcut = new()
        {
            Target = Environment.ProcessPath,
            Arguments = PicaLaunchArguments.ClipboardAgentArgument
        })
        {
            shortcut.Save(path);
        }

        byte[] previousShortcut = await File.ReadAllBytesAsync(path);
        PicaClipboardAgent agent = CreateAgent(stateService, path);
        await using WindowsClipboardHotKey owner = new(() => { });
        await using WindowsClipboardHotKey probe = new(() => { });
        await owner.SetAsync(previous, CancellationToken.None);

        Func<Task> change = () => agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetGesture, candidate),
            owner, CancellationToken.None);

        await change.Should().ThrowAsync<IOException>();
        (await File.ReadAllBytesAsync(path)).Should().Equal(previousShortcut);
        await owner.ValidateAsync(previous, CancellationToken.None);
        await probe.ValidateAsync(candidate, CancellationToken.None);
        (await stateService.LoadAsync(CancellationToken.None)).ClipboardShortcut.Should().Be(previous);
    }

    [Fact]
    public async Task ApplyAsync_WhenShortcutWriteFails_KeepsPreviousSettingAndRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory directory = new();
        PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
            NullLogger<PicaDesktopStateService>.Instance);
        PicaClipboardShortcutGesture previous = CreateGesture(Key.F23);
        await stateService.UpdateAsync(state => state.ClipboardShortcut = previous, CancellationToken.None);
        string path = Path.Combine(directory.DirectoryPath, "missing", "clipboard.lnk");
        PicaClipboardAgent agent = CreateAgent(stateService, path);
        await using WindowsClipboardHotKey owner = new(() => { });
        await using WindowsClipboardHotKey probe = new(() => { });

        Func<Task> enable = () => agent.ApplyAsync(new PicaClipboardAgentRequest(PicaClipboardAgentOperation.SetEnabled, IsEnabled: true),
            owner, CancellationToken.None);

        await enable.Should().ThrowAsync<Exception>();
        (await stateService.LoadAsync(CancellationToken.None)).IsClipboardShortcutEnabled.Should().BeFalse();
        await probe.ValidateAsync(previous, CancellationToken.None);
    }

    private static PicaClipboardAgent CreateAgent(IPicaDesktopStateService stateService, string shortcutPath)
    {
        return new PicaClipboardAgent(stateService, NullLogger<PicaClipboardAgent>.Instance,
            shortcutPath, Environment.ProcessPath ?? throw new InvalidOperationException("Test process path is unavailable."),
            _ => Task.CompletedTask, clearWindowHotKeys: () => { });
    }

    private static PicaClipboardShortcutGesture CreateGesture(Key key)
    {
        return new PicaClipboardShortcutGesture(KeyInterop.VirtualKeyFromKey(key),
            PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift);
    }
}

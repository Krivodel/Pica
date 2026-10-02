using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;

namespace Pica.Desktop.Tests.Services.Background;

[Collection(WindowsShortcutCollection.Name)]
public sealed class PicaWindowClipboardActivationTests
{
    [Fact]
    public async Task ReleaseWindowHotKey_WithRepeatedExplorerAssignments_RemovesWindowActivationShortcut()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using WindowsShortcutTestWindow window = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        nint handle = await window.Handle.WaitAsync(timeout.Token);

        foreach (short hotKey in new short[] { 0x86, 0x87, 0x86 })
        {
            for (int i = 0; i < 4; i++)
            {
                WindowsShortcutTestWindow.SetHotKey(handle, hotKey).Should().Be((nint)1);
                WindowsShortcutTestWindow.ReadHotKey(handle).Should().Be(hotKey);

                PicaWindowClipboardActivation.ReleaseWindowHotKey(handle);

                WindowsShortcutTestWindow.ReadHotKey(handle).Should().Be(0);
            }
        }
    }

    [Fact]
    public async Task IsWindowHotKeyAssignment_WithExplorerAssignments_RejectsWindowHotKeysAndAllowsRelease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using WindowsShortcutTestWindow window = new(rejectWindowHotKeys: true);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        nint handle = await window.Handle.WaitAsync(timeout.Token);
        PicaClipboardShortcutGesture gesture = new(0x55,
            PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift);

        WindowsShortcutTestWindow.SetHotKey(handle, gesture.ShellHotKey).Should().Be(nint.Zero);
        WindowsShortcutTestWindow.SetHotKey(handle, 0).Should().Be((nint)1);

        WindowsShortcutTestWindow.ReadHotKey(handle).Should().Be(0);
    }
}

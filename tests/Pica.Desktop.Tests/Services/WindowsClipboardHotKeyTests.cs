using Avalonia.Input;
using Avalonia.Win32.Input;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Tests.Services.Background;

namespace Pica.Desktop.Tests.Services;

[Collection(WindowsShortcutCollection.Name)]
public sealed class WindowsClipboardHotKeyTests
{
    [Fact]
    public async Task SetAsync_WithRepeatedChangesAndOwnValidation_ReleasesPreviousRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        PicaClipboardShortcutGesture first = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture second = CreateGesture(Key.F24);
        await using WindowsClipboardHotKey owner = new(() => { });
        await using WindowsClipboardHotKey probe = new(() => { });

        await owner.SetAsync(first, CancellationToken.None);
        await owner.ValidateAsync(first, CancellationToken.None);
        await owner.ValidateAsync(first with { IsExtended = true }, CancellationToken.None);
        await owner.SetAsync(second, CancellationToken.None);
        await probe.SetAsync(first, CancellationToken.None);
        await probe.SetAsync(null, CancellationToken.None);
        await owner.SetAsync(first, CancellationToken.None);
        await probe.SetAsync(second, CancellationToken.None);
        await owner.SetAsync(null, CancellationToken.None);
        await probe.ValidateAsync(first, CancellationToken.None);

        Func<Task> occupied = () => owner.ValidateAsync(second, CancellationToken.None);
        await occupied.Should().ThrowAsync<PicaShortcutException>();
    }

    [Fact]
    public async Task SetAsync_WhenCandidateIsOccupied_PreservesCurrentRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        PicaClipboardShortcutGesture first = CreateGesture(Key.F23);
        PicaClipboardShortcutGesture occupied = CreateGesture(Key.F24);
        await using WindowsClipboardHotKey owner = new(() => { });
        await using WindowsClipboardHotKey other = new(() => { });
        await owner.SetAsync(first, CancellationToken.None);
        await other.SetAsync(occupied, CancellationToken.None);

        Func<Task> change = () => owner.SetAsync(occupied, CancellationToken.None);

        (await change.Should().ThrowAsync<PicaShortcutException>()).Which.Failure.Should().Be(PicaShortcutFailure.Occupied);
        await owner.ValidateAsync(first, CancellationToken.None);
        Func<Task> original = () => other.ValidateAsync(first, CancellationToken.None);
        await original.Should().ThrowAsync<PicaShortcutException>();
    }

    [Fact]
    public async Task DisposeAsync_AfterRegistration_ReleasesHotKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        PicaClipboardShortcutGesture gesture = CreateGesture(Key.F24);
        WindowsClipboardHotKey owner = new(() => { });
        await owner.SetAsync(gesture, CancellationToken.None);

        await owner.DisposeAsync();

        await using WindowsClipboardHotKey probe = new(() => { });
        await probe.SetAsync(gesture, CancellationToken.None);
    }

    private static PicaClipboardShortcutGesture CreateGesture(Key key)
    {
        return new PicaClipboardShortcutGesture(KeyInterop.VirtualKeyFromKey(key),
            PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift);
    }
}

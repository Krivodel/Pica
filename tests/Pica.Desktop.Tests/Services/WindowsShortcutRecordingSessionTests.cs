using Avalonia.Input;
using Avalonia.Win32.Input;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class WindowsShortcutRecordingSessionTests
{
    [Theory]
    [InlineData(Key.LeftCtrl, Key.LeftAlt, (int)PicaShortcutModifiers.Alt)]
    [InlineData(Key.RightCtrl, Key.RightAlt, (int)PicaShortcutModifiers.Alt)]
    [InlineData(Key.LeftCtrl, Key.LeftShift, (int)PicaShortcutModifiers.Shift)]
    [InlineData(Key.RightCtrl, Key.RightShift, (int)PicaShortcutModifiers.Shift)]
    public void ProcessKey_WithEitherModifierSide_CompletesOnlyAfterMainKeyRelease(
        Key control, Key modifier, int expectedModifier)
    {
        WindowsShortcutRecordingSession session = new(Array.Empty<int>());
        int mainKey = KeyInterop.VirtualKeyFromKey(Key.V);

        session.ProcessKey(KeyInterop.VirtualKeyFromKey(control), true, false);
        session.ProcessKey(KeyInterop.VirtualKeyFromKey(modifier), true, true);
        session.ProcessKey(mainKey, true, false);
        session.ProcessKey(mainKey, true, false);
        session.IsComplete.Should().BeFalse();
        session.ProcessKey(KeyInterop.VirtualKeyFromKey(modifier), false, true);
        session.IsComplete.Should().BeFalse();
        session.ProcessKey(mainKey, false, false);

        session.IsComplete.Should().BeTrue();
        session.Gesture.Should().Be(new PicaClipboardShortcutGesture(mainKey,
            PicaShortcutModifiers.Control | (PicaShortcutModifiers)expectedModifier));
    }

    [Fact]
    public void ProcessKey_WithOnlyModifiers_RemainsListening()
    {
        WindowsShortcutRecordingSession session = new(Array.Empty<int>());
        int key = KeyInterop.VirtualKeyFromKey(Key.RightCtrl);

        session.ProcessKey(key, true, true);
        session.ProcessKey(key, false, true);

        session.IsComplete.Should().BeFalse();
        session.Gesture.Should().BeNull();
    }

    [Fact]
    public void ProcessKey_WithInitiallyHeldModifier_PreservesExtendedKey()
    {
        int control = KeyInterop.VirtualKeyFromKey(Key.LeftCtrl);
        WindowsShortcutRecordingSession session = new(new int[] { control });
        int key = KeyInterop.VirtualKeyFromKey(Key.Insert);

        session.ProcessKey(key, true, true);
        session.ProcessKey(control, false, false);
        session.ProcessKey(key, false, true);

        session.Gesture.Should().Be(new PicaClipboardShortcutGesture(key, PicaShortcutModifiers.Control, true));
        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void ProcessKey_WithEscape_CancelsInsteadOfCapturing()
    {
        WindowsShortcutRecordingSession session = new(Array.Empty<int>());

        session.ProcessKey(KeyInterop.VirtualKeyFromKey(Key.Escape), true, false);

        session.IsCanceled.Should().BeTrue();
        session.Gesture.Should().BeNull();
    }

    [Fact]
    public void ProcessKey_WithWindowsModifier_RetainsModifierForValidation()
    {
        WindowsShortcutRecordingSession session = new(Array.Empty<int>());
        int mainKey = KeyInterop.VirtualKeyFromKey(Key.J);

        session.ProcessKey(KeyInterop.VirtualKeyFromKey(Key.LWin), true, true);
        session.ProcessKey(mainKey, true, false);
        session.ProcessKey(mainKey, false, false);

        session.Gesture?.Modifiers.Should().Be(PicaShortcutModifiers.Windows);
        session.Gesture?.IsSupported.Should().BeFalse();
    }
}

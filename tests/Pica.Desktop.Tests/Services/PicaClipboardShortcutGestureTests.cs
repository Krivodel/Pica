using Avalonia.Input;
using Avalonia.Win32.Input;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaClipboardShortcutGestureTests
{
    [Fact]
    public void Default_WithInitialSettings_UsesControlShiftV()
    {
        PicaClipboardShortcutGesture gesture = PicaClipboardShortcutGesture.Default;

        gesture.Key.Should().Be(Key.V);
        gesture.ToKeyModifiers().Should().Be(KeyModifiers.Control | KeyModifiers.Shift);
        gesture.RegistrationModifiers.Should().Be(6);
    }

    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.F11)]
    [InlineData(Key.F13)]
    [InlineData(Key.F24)]
    public void Validate_WithUnmodifiedFunctionKey_AcceptsGesture(Key key)
    {
        PicaClipboardShortcutGesture gesture = new(KeyInterop.VirtualKeyFromKey(key), PicaShortcutModifiers.None);

        Action validate = gesture.Validate;

        validate.Should().NotThrow();
    }

    [Theory]
    [InlineData(Key.F12, (int)(PicaShortcutModifiers.None))]
    [InlineData(Key.F12, (int)(PicaShortcutModifiers.Control))]
    [InlineData(Key.F1, (int)(PicaShortcutModifiers.Shift))]
    [InlineData(Key.V, (int)(PicaShortcutModifiers.None))]
    [InlineData(Key.V, (int)(PicaShortcutModifiers.Shift))]
    [InlineData(Key.V, (int)(PicaShortcutModifiers.Windows | PicaShortcutModifiers.Control))]
    [InlineData(Key.LeftCtrl, (int)(PicaShortcutModifiers.Alt))]
    [InlineData(Key.Escape, (int)(PicaShortcutModifiers.Control))]
    [InlineData(Key.Tab, (int)(PicaShortcutModifiers.Alt))]
    [InlineData(Key.Space, (int)(PicaShortcutModifiers.Control))]
    [InlineData(Key.F4, (int)(PicaShortcutModifiers.Alt))]
    [InlineData(Key.F4, (int)(PicaShortcutModifiers.Alt | PicaShortcutModifiers.Shift))]
    [InlineData(Key.Delete, (int)(PicaShortcutModifiers.Control | PicaShortcutModifiers.Alt))]
    public void Validate_WithUnsupportedOrReservedKey_RejectsGesture(Key key, int modifiers)
    {
        PicaClipboardShortcutGesture gesture = new(KeyInterop.VirtualKeyFromKey(key), (PicaShortcutModifiers)modifiers);

        Action validate = gesture.Validate;

        validate.Should().Throw<PicaShortcutException>().Which.Failure.Should().Be(PicaShortcutFailure.Unsupported);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(256)]
    public void Validate_WithInvalidVirtualKey_RejectsGesture(int virtualKey)
    {
        PicaClipboardShortcutGesture gesture = new(virtualKey, PicaShortcutModifiers.Control);

        Action validate = gesture.Validate;

        validate.Should().Throw<PicaShortcutException>();
    }

    [Theory]
    [InlineData(Key.C, PhysicalKey.C, KeyModifiers.Control, true)]
    [InlineData(Key.C, PhysicalKey.C, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.A, PhysicalKey.A, KeyModifiers.Alt, true)]
    [InlineData(Key.F, PhysicalKey.F, KeyModifiers.None, true)]
    [InlineData(Key.F, PhysicalKey.F, KeyModifiers.Control, false)]
    [InlineData(Key.F, PhysicalKey.F, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.T, PhysicalKey.T, KeyModifiers.Control, false)]
    [InlineData(Key.C, PhysicalKey.C, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.C, PhysicalKey.C, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Left, PhysicalKey.ArrowLeft, KeyModifiers.Alt, true)]
    [InlineData(Key.OemPeriod, PhysicalKey.Period, KeyModifiers.Control, true)]
    [InlineData(Key.None, PhysicalKey.Comma, KeyModifiers.Control, true)]
    [InlineData(Key.V, PhysicalKey.V, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.F8, PhysicalKey.F8, KeyModifiers.None, false)]
    [InlineData(Key.X, PhysicalKey.X, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void ConflictsWithClipboard_WithViewerBinding_UsesAssignedCommandModifiers(
        Key key, PhysicalKey physicalKey, KeyModifiers modifiers, bool expected)
    {
        bool conflicts = ViewerKeyboardShortcutPolicy.ConflictsWithClipboard(key, physicalKey, modifiers);

        conflicts.Should().Be(expected);
    }
}

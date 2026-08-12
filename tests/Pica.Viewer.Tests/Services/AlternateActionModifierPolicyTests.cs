using Avalonia.Input;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class AlternateActionModifierPolicyTests
{
    [Theory]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    public void IsActive_WithContentNavigationModifier_ReturnsTrue(
        KeyModifiers modifiers)
    {
        bool isActive =
            AlternateActionModifierPolicy.IsActive(modifiers);

        isActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WithoutModifier_ReturnsFalse()
    {
        bool isActive = AlternateActionModifierPolicy.IsActive(
            KeyModifiers.None);

        isActive.Should().BeFalse();
    }
}

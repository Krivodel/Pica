using Avalonia;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public class ViewerPointerMotionTests
{
    [Fact]
    public void HasMoved_WithoutPreviousPosition_ReturnsFalse()
    {
        PixelPoint currentPosition = new(100, 200);

        bool hasMoved = ViewerPointerMotion.HasMoved(null, currentPosition);

        hasMoved.Should().BeFalse();
    }

    [Fact]
    public void HasMoved_WithSameScreenPosition_ReturnsFalse()
    {
        PixelPoint position = new(100, 200);

        bool hasMoved = ViewerPointerMotion.HasMoved(position, position);

        hasMoved.Should().BeFalse();
    }

    [Fact]
    public void HasMoved_WithDifferentScreenPosition_ReturnsTrue()
    {
        PixelPoint previousPosition = new(100, 200);
        PixelPoint currentPosition = new(101, 200);

        bool hasMoved = ViewerPointerMotion.HasMoved(previousPosition, currentPosition);

        hasMoved.Should().BeTrue();
    }
}

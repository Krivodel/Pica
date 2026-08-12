using FluentAssertions;
using Xunit;

using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ViewerChromeVisibilityControllerTests
{
    [Theory]
    [InlineData(210d, 198d)]
    [InlineData(8d, 0d)]
    public void CalculateBottomRevealTop_WithPanelPosition_StartsAbovePanel(
        double panelTop,
        double expectedRevealTop)
    {
        double revealTop =
            ViewerChromeVisibilityController
                .CalculateBottomRevealTop(panelTop);

        revealTop.Should().Be(expectedRevealTop);
    }
}

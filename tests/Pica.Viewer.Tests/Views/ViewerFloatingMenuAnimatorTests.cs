using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ViewerFloatingMenuAnimatorTests
{
    [Fact]
    public void Open_WhenClosingIsPending_KeepsReopenedMenuInteractive()
    {
        ControlledUiFrameScheduler frameScheduler = new();
        ViewerFrameAnimationRunner animationRunner = new(frameScheduler);
        Border menu = new()
        {
            IsVisible = true
        };
        using ViewerFloatingMenuAnimator animator = new(
            menu,
            animationRunner,
            1d,
            0d);
        Point pointerPosition = new(100d, 100d);
        Point menuPosition = new(108d, 108d);
        Size menuSize = new(200d, 100d);

        animator.Open(pointerPosition, menuPosition, menuSize);
        animator.Close();
        animator.Open(pointerPosition, menuPosition, menuSize);
        frameScheduler.RunNext(TimeSpan.Zero);
        frameScheduler.RunNext(TimeSpan.Zero);

        menu.IsVisible.Should().BeTrue();
        menu.IsHitTestVisible.Should().BeTrue();
        menu.Clip.Should().NotBeNull();
    }

    [Theory]
    [InlineData(108d, 108d, (int)ViewerFloatingMenuRevealOrigin.TopLeft)]
    [InlineData(52d, 108d, (int)ViewerFloatingMenuRevealOrigin.TopRight)]
    [InlineData(108d, 62d, (int)ViewerFloatingMenuRevealOrigin.BottomLeft)]
    [InlineData(52d, 62d, (int)ViewerFloatingMenuRevealOrigin.BottomRight)]
    public void ResolveOrigin_WhenMenuPlacedAroundPointer_UsesNearestCorner(
        double menuX,
        double menuY,
        int expectedOriginValue)
    {
        Point pointerPosition = new(100d, 100d);
        Point menuPosition = new(menuX, menuY);
        Size menuSize = new(40d, 30d);

        ViewerFloatingMenuRevealOrigin origin =
            ViewerFloatingMenuAnimator.ResolveOrigin(
                pointerPosition,
                menuPosition,
                menuSize);

        origin.Should().Be((ViewerFloatingMenuRevealOrigin)expectedOriginValue);
    }

    [Theory]
    [InlineData(40d, 40d, (int)ViewerFloatingMenuRevealOrigin.TopLeft)]
    [InlineData(260d, 40d, (int)ViewerFloatingMenuRevealOrigin.TopRight)]
    [InlineData(40d, 160d, (int)ViewerFloatingMenuRevealOrigin.BottomLeft)]
    [InlineData(260d, 160d, (int)ViewerFloatingMenuRevealOrigin.BottomRight)]
    public void ResolveNearestOrigin_WhenSubmenuOpensAroundAnchor_UsesClosestCorner(
        double anchorX,
        double anchorY,
        int expectedOriginValue)
    {
        Point anchorPosition = new(anchorX, anchorY);
        Size anchorSize = new(20d, 20d);
        Point menuPosition = new(100d, 50d);
        Size menuSize = new(100d, 100d);

        ViewerFloatingMenuRevealOrigin origin =
            ViewerFloatingMenuAnimator.ResolveNearestOrigin(
                anchorPosition,
                anchorSize,
                menuPosition,
                menuSize);

        origin.Should().Be((ViewerFloatingMenuRevealOrigin)expectedOriginValue);
    }

    [Theory]
    [InlineData((int)ViewerFloatingMenuRevealOrigin.TopLeft, 0d, 0d)]
    [InlineData((int)ViewerFloatingMenuRevealOrigin.TopRight, 100d, 0d)]
    [InlineData((int)ViewerFloatingMenuRevealOrigin.BottomLeft, 0d, 70d)]
    [InlineData((int)ViewerFloatingMenuRevealOrigin.BottomRight, 100d, 70d)]
    public void CalculateRevealBounds_AtStart_AnchorsToOrigin(
        int originValue,
        double expectedX,
        double expectedY)
    {
        Size menuSize = new(200d, 100d);

        Rect bounds = ViewerFloatingMenuAnimator.CalculateRevealBounds(
            menuSize,
            ViewerFloatingMenuAnimator.InitialWidthRatio,
            ViewerFloatingMenuAnimator.InitialHeightRatio,
            (ViewerFloatingMenuRevealOrigin)originValue);

        bounds.Should().Be(new Rect(expectedX, expectedY, 100d, 30d));
    }
}

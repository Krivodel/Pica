using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageViewerStateTests
{
    [Fact]
    public void CreateSnapshot_WithRememberedPlacement_PreservesSettingsAndPlacement()
    {
        ImageViewerState initialState =
            ImageViewerStateTestFactory.CreateRememberedPlacementState();
        ImageViewerState settings = initialState.CreateCopy();

        ImageViewerState result = settings.CreateSnapshot(
            true,
            -1200,
            80,
            900d,
            506.25d);

        result.Should().BeEquivalentTo(initialState);
    }

    [Fact]
    public void CreateSnapshot_WithoutRememberedPlacement_ClearsPlacement()
    {
        ImageViewerState initialState = new()
        {
            RememberWindowPlacement = false
        };
        ImageViewerState settings = initialState.CreateCopy();

        ImageViewerState result = settings.CreateSnapshot(
            true,
            100,
            200,
            900d,
            600d);

        result.RememberWindowPlacement.Should().BeFalse();
        result.IsWindowed.Should().BeFalse();
        result.WindowX.Should().BeNull();
        result.WindowY.Should().BeNull();
        result.WindowWidth.Should().BeNull();
        result.WindowHeight.Should().BeNull();
    }
}

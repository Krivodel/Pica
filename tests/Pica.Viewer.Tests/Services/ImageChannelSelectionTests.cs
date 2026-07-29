using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageChannelSelectionTests
{
    [Fact]
    public void Enter_WhenInactive_SelectsRedChannel()
    {
        ImageChannelSelection selection = new();

        selection.Enter();

        selection.IsActive.Should().BeTrue();
        selection.SelectedChannel.Should().Be(ImageChannel.Red);
        selection.IsAvailabilityKnown.Should().BeFalse();
    }

    [Fact]
    public void Navigate_WithColorChannels_WrapsFromRedToBlue()
    {
        ImageChannelSelection selection = new();
        selection.Enter();

        selection.Navigate(-1);

        selection.SelectedChannel.Should().Be(ImageChannel.Blue);
    }

    [Fact]
    public void Navigate_WithAlphaChannel_WrapsFromRedToAlpha()
    {
        ImageChannelSelection selection = new();
        selection.Enter();
        selection.SetHasAlpha(true);

        selection.Navigate(-1);

        selection.SelectedChannel.Should().Be(ImageChannel.Alpha);
        selection.IsAvailabilityKnown.Should().BeTrue();
    }

    [Fact]
    public void Exit_WhenActive_ClearsSelectedChannel()
    {
        ImageChannelSelection selection = new();
        selection.Enter();

        selection.Exit();

        selection.IsActive.Should().BeFalse();
        selection.SelectedChannel.Should().BeNull();
    }
}

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageViewerInputPolicyTests
{
    [Fact]
    public void ResolveEscapeAction_WithSelectionAndChannelMode_CancelsSelection()
    {
        ImageViewerInputState state = new(
            false,
            true,
            true);

        ViewerEscapeAction action =
            ImageViewerInputPolicy.ResolveEscapeAction(state);

        action.Should().Be(ViewerEscapeAction.CancelAreaSelection);
    }

    [Fact]
    public void ResolveEscapeAction_WithSettingsSelectionAndChannelMode_HidesSettings()
    {
        ImageViewerInputState state = new(
            true,
            true,
            true);

        ViewerEscapeAction action =
            ImageViewerInputPolicy.ResolveEscapeAction(state);

        action.Should().Be(ViewerEscapeAction.HideSettings);
    }

    [Fact]
    public void ResolveEscapeAction_WithOnlyChannelMode_ExitsChannelMode()
    {
        ImageViewerInputState state = new(
            false,
            false,
            true);

        ViewerEscapeAction action =
            ImageViewerInputPolicy.ResolveEscapeAction(state);

        action.Should().Be(ViewerEscapeAction.ExitChannelMode);
    }

    [Fact]
    public void ResolveEscapeAction_WithNoOverlayMode_ClosesViewer()
    {
        ImageViewerInputState state = new(
            false,
            false,
            false);

        ViewerEscapeAction action =
            ImageViewerInputPolicy.ResolveEscapeAction(state);

        action.Should().Be(ViewerEscapeAction.CloseViewer);
    }
}

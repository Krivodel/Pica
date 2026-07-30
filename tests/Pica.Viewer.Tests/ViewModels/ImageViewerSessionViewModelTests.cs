using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerSessionViewModelTests
{
    private static readonly Guid FirstItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_WithSelectedItem_ExposesSelectedItem()
    {
        PicaViewerRequest request = CreateRequest(SecondItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, true));

        viewModel.SelectedItem.Should().Be(request.Items[1]);
        viewModel.SelectedIndex.Should().Be(1);
        viewModel.IsFilteringEnabled.Should().BeTrue();
        viewModel.IsMainImageModeActive.Should().BeTrue();
    }

    [Fact]
    public void SelectChannelImageModeCommand_WhenMainMode_SelectsRedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.IsChannelModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().Be(ImageChannel.Red);
        viewModel.IsChannelAvailabilityKnown.Should().BeFalse();
    }

    [Fact]
    public void ToggleImageModeCommand_WhenChannelMode_ReturnsToMainMode()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.ToggleImageModeCommand.Execute(null);

        viewModel.IsMainImageModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().BeNull();
    }

    [Fact]
    public void ToggleImageModeCommand_WhenMainMode_SelectsRedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.ToggleImageModeCommand.Execute(null);

        viewModel.IsChannelModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().Be(ImageChannel.Red);
    }

    [Fact]
    public void NavigateCommand_WithMainMode_NavigatesImagesAndRecordsDirection()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        viewModel.NavigateCommand.Execute(1);

        viewModel.SelectedIndex.Should().Be(1);
        viewModel.SelectedItem?.Id.Should().Be(SecondItemId);
        viewModel.PreferredNavigationDirection.Should().Be(1);
    }

    [Fact]
    public void NavigateCommand_WithColorChannels_WrapsFromRedToBlue()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.NavigateCommand.Execute(-1);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Blue);
        viewModel.SelectedIndex.Should().Be(0);
        viewModel.PreferredNavigationDirection.Should().Be(1);
    }

    [Fact]
    public void NavigateCommand_WithAlphaChannel_WrapsFromRedToAlpha()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        viewModel.SelectChannelImageModeCommand.Execute(null);
        session.SetHasAlpha(true);

        viewModel.NavigateCommand.Execute(-1);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Alpha);
        viewModel.IsChannelAvailabilityKnown.Should().BeTrue();
    }

    [Fact]
    public void NavigateCommand_WithZeroDirection_ThrowsArgumentOutOfRangeException()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();

        Action act = () => viewModel.NavigateCommand.Execute(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChannelAvailability_WhenAlphaBecomesUnavailable_SelectsBlueChannel()
    {
        ImageViewerSession session = CreateSession();
        using ImageViewerSessionViewModel viewModel = new(session);
        viewModel.SelectChannelImageModeCommand.Execute(null);
        session.SetHasAlpha(true);
        viewModel.NavigateCommand.Execute(-1);

        session.SetHasAlpha(false);

        viewModel.SelectedChannel.Should().Be(ImageChannel.Blue);
        viewModel.IsChannelAvailabilityKnown.Should().BeTrue();
    }

    [Fact]
    public void SelectMainImageModeCommand_WhenChannelMode_ClearsSelectedChannel()
    {
        ImageViewerSessionViewModel viewModel = CreateViewModel();
        viewModel.SelectChannelImageModeCommand.Execute(null);

        viewModel.SelectMainImageModeCommand.Execute(null);

        viewModel.IsMainImageModeActive.Should().BeTrue();
        viewModel.SelectedChannel.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithUnknownSelectedItem_SelectsFirstItem()
    {
        Guid unknownItemId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");
        PicaViewerRequest request = CreateRequest(unknownItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.SelectedIndex.Should().Be(0);
        viewModel.SelectedItem?.Id.Should().Be(FirstItemId);
    }

    [Fact]
    public void Constructor_WithoutItems_HasNoSelectedItem()
    {
        PicaViewerRequest request = new(
            Array.Empty<PicaImageItem>(),
            FirstItemId);

        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.SelectedIndex.Should().Be(-1);
        viewModel.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void NavigateCommand_WithoutItems_KeepsNoSelection()
    {
        PicaViewerRequest request = new(
            Array.Empty<PicaImageItem>(),
            FirstItemId);
        ImageViewerSessionViewModel viewModel = new(
            new ImageViewerSession(request, false));

        viewModel.NavigateCommand.Execute(1);

        viewModel.SelectedIndex.Should().Be(-1);
        viewModel.SelectedItem.Should().BeNull();
    }

    private static ImageViewerSessionViewModel CreateViewModel(
        bool isFilteringEnabled = false)
    {
        ImageViewerSession session = new(
            CreateRequest(FirstItemId),
            isFilteringEnabled);

        return new ImageViewerSessionViewModel(session);
    }

    private static ImageViewerSession CreateSession()
    {
        return new ImageViewerSession(
            CreateRequest(FirstItemId),
            false);
    }

    private static PicaViewerRequest CreateRequest(Guid selectedItemId)
    {
        PicaImageItem[] items =
        [
            new PicaImageItem(
                FirstItemId,
                "first.png",
                "first.png"),
            new PicaImageItem(
                SecondItemId,
                "second.png",
                "second.png")
        ];

        return new PicaViewerRequest(
            items,
            selectedItemId);
    }
}

using FluentAssertions;
using Xunit;

using Pica.Viewer.Helpers;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Helpers;

public sealed class ImageViewerInformationFormatterTests
{
    private static readonly DateTime ModificationDate = new(
        2026,
        7,
        27,
        19,
        28,
        0);

    [Fact]
    public void Format_WithAllInformation_ReturnsCombinedText()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(1920, 1080),
            null,
            ModificationDate,
            CreateOptions());

        result.Should().Be(
            "image.png · 27.07.2026 19:28 · 1920×1080");
    }

    [Fact]
    public void Format_WithNameWithoutFormat_ReturnsNameWithoutExtension()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(),
            null,
            null,
            CreateOptions(
                showFormat: false,
                showResolution: false,
                showModificationDate: false));

        result.Should().Be("image");
    }

    [Fact]
    public void Format_WithFormatWithoutName_ReturnsLowercaseFormat()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.JPG",
            new ImageDimensions(),
            null,
            null,
            CreateOptions(
                showName: false,
                showResolution: false,
                showModificationDate: false));

        result.Should().Be("jpg");
    }

    [Fact]
    public void Format_WithoutNameAndFormat_ReturnsRemainingInformation()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(640, 480),
            null,
            ModificationDate,
            CreateOptions(
                showName: false,
                showFormat: false));

        result.Should().Be("27.07.2026 19:28 · 640×480");
    }

    [Fact]
    public void Format_WithoutAvailableOptionalInformation_OmitsIt()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(),
            null,
            null,
            CreateOptions());

        result.Should().Be("image.png");
    }

    [Fact]
    public void Format_WithAllOptionsDisabled_ReturnsEmptyText()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(1920, 1080),
            null,
            ModificationDate,
            CreateOptions(
                showName: false,
                showFormat: false,
                showResolution: false,
                showModificationDate: false));

        result.Should().BeEmpty();
    }

    [Fact]
    public void Format_WithSelectedChannel_PlacesChannelAfterResolution()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(1920, 1080),
            ImageChannel.Red,
            ModificationDate,
            CreateOptions());

        result.Should().Be(
            "image.png · 27.07.2026 19:28 · 1920×1080 · Канал R");
    }

    [Fact]
    public void Format_WithOnlySelectedChannel_ReturnsChannelInformation()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new ImageDimensions(1920, 1080),
            ImageChannel.Green,
            ModificationDate,
            CreateOptions(
                showName: false,
                showFormat: false,
                showResolution: false,
                showModificationDate: false));

        result.Should().Be("Канал G");
    }

    private static ImageViewerInformationOptions CreateOptions(
        bool showName = true,
        bool showFormat = true,
        bool showResolution = true,
        bool showModificationDate = true)
    {
        return new ImageViewerInformationOptions(
            showName,
            showFormat,
            showResolution,
            showModificationDate);
    }
}

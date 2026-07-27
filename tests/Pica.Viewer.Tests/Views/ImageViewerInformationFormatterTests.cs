using Avalonia;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

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
            new PixelSize(1920, 1080),
            ModificationDate,
            CreateOptions());

        result.Should().Be(
            "image.png · 1920×1080 · 27.07.2026 19:28");
    }

    [Fact]
    public void Format_WithNameWithoutFormat_ReturnsNameWithoutExtension()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new PixelSize(),
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
            new PixelSize(),
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
            new PixelSize(640, 480),
            ModificationDate,
            CreateOptions(
                showName: false,
                showFormat: false));

        result.Should().Be("640×480 · 27.07.2026 19:28");
    }

    [Fact]
    public void Format_WithoutAvailableOptionalInformation_OmitsIt()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new PixelSize(),
            null,
            CreateOptions());

        result.Should().Be("image.png");
    }

    [Fact]
    public void Format_WithAllOptionsDisabled_ReturnsEmptyText()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new PixelSize(1920, 1080),
            ModificationDate,
            CreateOptions(
                showName: false,
                showFormat: false,
                showResolution: false,
                showModificationDate: false));

        result.Should().BeEmpty();
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

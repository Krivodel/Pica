using FluentAssertions;
using Xunit;

using Pica.Viewer.Helpers;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Helpers;

public sealed class ImageContentNavigationFormatterTests
{
    [Fact]
    public void FormatContent_WithSelectedImageInMixedFile_ReturnsAnimationCountAfterSelection()
    {
        string result = ImageContentNavigationFormatter.FormatContent(
            ImageContentGroupKind.StillImages,
            1,
            3,
            3,
            3);

        result.Should().Be("Изображение 1/3 · Анимаций: 3");
    }

    [Fact]
    public void FormatContent_WithSelectedAnimationInMixedFile_ReturnsImageCountBeforeSelection()
    {
        string result = ImageContentNavigationFormatter.FormatContent(
            ImageContentGroupKind.Animation,
            2,
            3,
            3,
            3);

        result.Should().Be("Изображений: 3 · Анимация 2/3");
    }

    [Fact]
    public void FormatContent_WithOnlyImages_ReturnsImageNumber()
    {
        string result = ImageContentNavigationFormatter.FormatContent(
            ImageContentGroupKind.StillImages,
            2,
            3,
            3,
            0);

        result.Should().Be("Изображение 2/3");
    }

    [Fact]
    public void FormatAnimationTime_WithSubMinuteDuration_ReturnsTenthsOfSecond()
    {
        string result =
            ImageContentNavigationFormatter.FormatAnimationTime(
                TimeSpan.FromMilliseconds(3400d),
                TimeSpan.FromMilliseconds(8100d));

        result.Should().Be("0:03.4 / 0:08.1");
    }

    [Fact]
    public void FormatAnimationTime_WithSubSecondDuration_ReturnsHundredthsOfSecond()
    {
        string result =
            ImageContentNavigationFormatter.FormatAnimationTime(
                TimeSpan.FromMilliseconds(100d),
                TimeSpan.FromMilliseconds(150d));

        result.Should().Be("0:00.10 / 0:00.15");
    }

    [Fact]
    public void FormatContentWidthReference_WithMixedContent_UsesWidestDigitPlaceholders()
    {
        string result = ImageContentNavigationFormatter
            .FormatContentWidthReference(
                ImageContentGroupKind.Animation,
                12,
                3,
                12);

        result.Should().Be("Изображений: 8 · Анимация 88/88");
    }

    [Fact]
    public void FormatAnimationTimeWidthReference_WithDuration_UsesDurationForBothParts()
    {
        string result = ImageContentNavigationFormatter
            .FormatAnimationTimeWidthReference(
                TimeSpan.FromMinutes(12d)
                    + TimeSpan.FromSeconds(3d));

        result.Should().Be("12:03 / 12:03");
    }
}

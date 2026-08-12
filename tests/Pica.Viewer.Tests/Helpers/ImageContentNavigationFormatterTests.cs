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
    public void FormatFrame_WithSelectedFrame_ReturnsFrameNumber()
    {
        string result = ImageContentNavigationFormatter.FormatFrame(
            4,
            12);

        result.Should().Be("Кадр 4/12");
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
    public void FormatFrameWidthReference_WithThreeDigitCount_UsesWidestDigitPlaceholders()
    {
        string result = ImageContentNavigationFormatter
            .FormatFrameWidthReference(120);

        result.Should().Be("Кадр 888/888");
    }
}

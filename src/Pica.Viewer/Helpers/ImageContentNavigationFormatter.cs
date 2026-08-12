using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Viewer.Helpers;

internal static class ImageContentNavigationFormatter
{
    private const string PartSeparator = " · ";
    private const char WidestDigit = '8';

    internal static string FormatContent(
        ImageContentGroupKind kind,
        int contentNumber,
        int contentCount,
        int imageCount,
        int animationCount)
    {
        ValidateNumber(contentNumber, contentCount);
        ValidateNonNegativeCount(imageCount, nameof(imageCount));
        ValidateNonNegativeCount(animationCount, nameof(animationCount));

        return kind switch
        {
            ImageContentGroupKind.StillImages =>
                FormatSelectedContent(
                    ViewerUiStrings.Image,
                    contentNumber,
                    contentCount,
                    null,
                    animationCount > 0
                        ? $"{ViewerUiStrings.AnimationCount} {animationCount}"
                        : null),
            ImageContentGroupKind.Animation =>
                FormatSelectedContent(
                    ViewerUiStrings.Animation,
                    contentNumber,
                    contentCount,
                    imageCount > 0
                        ? $"{ViewerUiStrings.ImageCount} {imageCount}"
                        : null,
                    null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The image content group kind is not supported.")
        };
    }

    internal static string FormatFrame(
        int frameNumber,
        int frameCount)
    {
        ValidateNumber(frameNumber, frameCount);

        return $"{ViewerUiStrings.Frame} {frameNumber}/{frameCount}";
    }

    internal static string FormatContentWidthReference(
        ImageContentGroupKind kind,
        int contentCount,
        int imageCount,
        int animationCount)
    {
        ValidatePositiveCount(contentCount, nameof(contentCount));
        ValidateNonNegativeCount(imageCount, nameof(imageCount));
        ValidateNonNegativeCount(animationCount, nameof(animationCount));
        string contentDigits = CreateWidthReferenceDigits(contentCount);
        string imageCountDigits = CreateWidthReferenceDigits(imageCount);
        string animationCountDigits =
            CreateWidthReferenceDigits(animationCount);

        return kind switch
        {
            ImageContentGroupKind.StillImages =>
                FormatSelectedContent(
                    ViewerUiStrings.Image,
                    contentDigits,
                    contentDigits,
                    null,
                    animationCount > 0
                        ? $"{ViewerUiStrings.AnimationCount} {animationCountDigits}"
                        : null),
            ImageContentGroupKind.Animation =>
                FormatSelectedContent(
                    ViewerUiStrings.Animation,
                    contentDigits,
                    contentDigits,
                    imageCount > 0
                        ? $"{ViewerUiStrings.ImageCount} {imageCountDigits}"
                        : null,
                    null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The image content group kind is not supported.")
        };
    }

    internal static string FormatFrameWidthReference(int frameCount)
    {
        ValidatePositiveCount(frameCount, nameof(frameCount));
        string frameDigits = CreateWidthReferenceDigits(frameCount);

        return $"{ViewerUiStrings.Frame} {frameDigits}/{frameDigits}";
    }

    private static void ValidatePositiveCount(
        int count,
        string parameterName)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                "The item count must be positive.");
        }
    }

    private static string FormatSelectedContent(
        string label,
        int contentNumber,
        int contentCount,
        string? countBefore,
        string? countAfter)
    {
        return FormatSelectedContent(
            label,
            contentNumber.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            contentCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            countBefore,
            countAfter);
    }

    private static string FormatSelectedContent(
        string label,
        string contentNumber,
        string contentCount,
        string? countBefore,
        string? countAfter)
    {
        string selectedContent =
            $"{label} {contentNumber}/{contentCount}";

        if (countBefore is not null)
        {
            return countBefore + PartSeparator + selectedContent;
        }

        return countAfter is not null
            ? selectedContent + PartSeparator + countAfter
            : selectedContent;
    }

    private static string CreateWidthReferenceDigits(int count)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        int digitCount = count
            .ToString(System.Globalization.CultureInfo.InvariantCulture)
            .Length;

        return new string(WidestDigit, digitCount);
    }

    private static void ValidateNonNegativeCount(
        int count,
        string parameterName)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                "The item count must not be negative.");
        }
    }

    private static void ValidateNumber(
        int number,
        int count)
    {
        ValidatePositiveCount(count, nameof(count));

        if ((number <= 0) || (number > count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                $"The item number must be between 1 and {count}.");
        }
    }
}

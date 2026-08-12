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

    internal static string FormatAnimationTime(
        TimeSpan position,
        TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The animation duration must not be negative.");
        }

        TimeSpan clampedPosition = TimeSpan.FromTicks(
            Math.Clamp(
                position.Ticks,
                0L,
                duration.Ticks));

        return $"{FormatTime(clampedPosition, duration)} / {FormatTime(duration, duration)}";
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

    internal static string FormatAnimationTimeWidthReference(
        TimeSpan duration)
    {
        string formattedDuration = FormatTime(
            duration,
            duration);

        return $"{formattedDuration} / {formattedDuration}";
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

    private static string FormatTime(
        TimeSpan time,
        TimeSpan totalDuration)
    {
        if (totalDuration.TotalSeconds < 1d)
        {
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"0:{time.Seconds:00}.{time.Milliseconds / 10:00}");
        }

        if (totalDuration.TotalHours >= 1d)
        {
            int totalHours = checked((int)Math.Floor(
                time.TotalHours));

            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{totalHours}:{time.Minutes:00}:{time.Seconds:00}");
        }

        if (totalDuration.TotalMinutes >= 1d)
        {
            int totalMinutes = checked((int)Math.Floor(
                time.TotalMinutes));

            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{totalMinutes}:{time.Seconds:00}");
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"0:{time.Seconds:00}.{time.Milliseconds / 100}");
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

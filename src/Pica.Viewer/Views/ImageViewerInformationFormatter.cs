using System.Globalization;

using Avalonia;

using Pica.Viewer.Resources;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

internal static class ImageViewerInformationFormatter
{
    private const string PartSeparator = " · ";
    private const string ModificationDateFormat = "dd.MM.yyyy HH:mm";

    internal static string Format(
        string fileName,
        PixelSize pixelSize,
        ImageChannel? selectedChannel,
        DateTime? modificationDate,
        ImageViewerInformationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(options);
        List<string> parts = [];

        string nameOrFormat = FormatNameOrFormat(fileName, options);

        if (!string.IsNullOrWhiteSpace(nameOrFormat))
        {
            parts.Add(nameOrFormat);
        }

        if (options.ShowModificationDate
            && modificationDate is { } date)
        {
            parts.Add(date.ToString(
                ModificationDateFormat,
                CultureInfo.InvariantCulture));
        }

        if (options.ShowResolution
            && (pixelSize.Width > 0)
            && (pixelSize.Height > 0))
        {
            parts.Add($"{pixelSize.Width}×{pixelSize.Height}");
        }

        if (selectedChannel is not null)
        {
            parts.Add($"{ViewerUiStrings.Channel} {selectedChannel.Code}");
        }

        return string.Join(PartSeparator, parts);
    }

    private static string FormatNameOrFormat(
        string fileName,
        ImageViewerInformationOptions options)
    {
        string extension = Path.GetExtension(fileName);
        string format = extension
            .TrimStart('.')
            .ToLowerInvariant();

        if (options.ShowName)
        {
            return options.ShowFormat
                ? fileName
                : Path.GetFileNameWithoutExtension(fileName);
        }

        return options.ShowFormat
            ? format
            : string.Empty;
    }
}

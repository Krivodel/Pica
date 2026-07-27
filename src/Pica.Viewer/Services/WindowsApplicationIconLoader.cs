using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

using DrawingBitmap = System.Drawing.Bitmap;

namespace Pica.Viewer.Services;

internal sealed class WindowsApplicationIconLoader
{
    private readonly ILogger<WindowsApplicationIconLoader> _logger;

    public WindowsApplicationIconLoader(ILogger<WindowsApplicationIconLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [SupportedOSPlatform("windows")]
    public byte[]? Load(
        string iconPath,
        int iconIndex,
        string fallbackExecutablePath)
    {
        string sourcePath = GetExistingSourcePath(iconPath, fallbackExecutablePath);

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        nint largeIcon = nint.Zero;
        nint smallIcon = nint.Zero;

        try
        {
            int extractedCount = WindowsShellNativeMethods.ExtractIconEx(
                sourcePath,
                iconIndex,
                out largeIcon,
                out smallIcon,
                1);
            nint iconHandle = smallIcon != nint.Zero ? smallIcon : largeIcon;

            if ((extractedCount <= 0) || (iconHandle == nint.Zero))
            {
                _logger.LogDebug(
                    "Windows did not return an icon for application handler index {IconIndex}",
                    iconIndex);

                return null;
            }

            using Icon borrowedIcon = Icon.FromHandle(iconHandle);
            using Icon icon = (Icon)borrowedIcon.Clone();
            using DrawingBitmap drawingBitmap = icon.ToBitmap();
            using MemoryStream stream = new();
            drawingBitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to convert a Windows application icon at index {IconIndex}",
                iconIndex);

            return null;
        }
        catch (ExternalException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to encode a Windows application icon at index {IconIndex}",
                iconIndex);

            return null;
        }
        finally
        {
            if (smallIcon != nint.Zero)
            {
                WindowsShellNativeMethods.DestroyIcon(smallIcon);
            }

            if ((largeIcon != nint.Zero) && (largeIcon != smallIcon))
            {
                WindowsShellNativeMethods.DestroyIcon(largeIcon);
            }
        }
    }

    private static string GetExistingSourcePath(
        string iconPath,
        string fallbackExecutablePath)
    {
        string expandedIconPath = Environment.ExpandEnvironmentVariables(iconPath.Trim('"'));

        if (File.Exists(expandedIconPath))
        {
            return expandedIconPath;
        }

        string expandedExecutablePath = Environment.ExpandEnvironmentVariables(
            fallbackExecutablePath.Trim('"'));

        return File.Exists(expandedExecutablePath)
            ? expandedExecutablePath
            : string.Empty;
    }
}

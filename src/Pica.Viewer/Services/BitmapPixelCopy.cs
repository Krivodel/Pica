using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pica.Viewer.Services;

internal static class BitmapPixelCopy
{
    public static WriteableBitmap CreateCopy(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        WriteableBitmap copy = new(
            bitmap.PixelSize,
            bitmap.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = copy.Lock();
        bitmap.CopyPixels(framebuffer);

        return copy;
    }

    public static WriteableBitmap CreateCrop(Bitmap bitmap, PixelRect sourceRect)
    {
        using RenderTargetBitmap renderedCrop = CreateRenderedCrop(bitmap, sourceRect);

        return CreateCopy(renderedCrop);
    }

    public static RenderTargetBitmap CreateRenderedCrop(Bitmap bitmap, PixelRect sourceRect)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ValidateSourceRect(bitmap.PixelSize, sourceRect);

        PixelSize targetSize = new(sourceRect.Width, sourceRect.Height);
        RenderTargetBitmap renderedCrop = new(targetSize, bitmap.Dpi);
        Rect sourceDipRect = ConvertPixelRectToDipRect(sourceRect, bitmap.Dpi);
        Rect targetDipRect = ConvertPixelRectToDipRect(
            new PixelRect(0, 0, sourceRect.Width, sourceRect.Height),
            bitmap.Dpi);
        bool isCompleted = false;

        try
        {
            using (DrawingContext context = renderedCrop.CreateDrawingContext())
            {
                RenderOptions renderOptions = new()
                {
                    BitmapInterpolationMode = BitmapInterpolationMode.None
                };
                using IDisposable renderOptionsScope = context.PushRenderOptions(renderOptions);
                context.DrawImage(bitmap, sourceDipRect, targetDipRect);
            }

            isCompleted = true;

            return renderedCrop;
        }
        finally
        {
            if (!isCompleted)
            {
                renderedCrop.Dispose();
            }
        }
    }

    public static PixelRect? NormalizeSourceRect(PixelSize sourceSize, PixelRect sourceRect)
    {
        if ((sourceSize.Width <= 0)
            || (sourceSize.Height <= 0)
            || (sourceRect.Width <= 0)
            || (sourceRect.Height <= 0))
        {
            return null;
        }

        int left = Math.Clamp(sourceRect.X, 0, sourceSize.Width);
        int top = Math.Clamp(sourceRect.Y, 0, sourceSize.Height);
        int right = (int)Math.Clamp(
            (long)sourceRect.X + sourceRect.Width,
            0L,
            sourceSize.Width);
        int bottom = (int)Math.Clamp(
            (long)sourceRect.Y + sourceRect.Height,
            0L,
            sourceSize.Height);

        if ((right <= left) || (bottom <= top))
        {
            return null;
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static Rect ConvertPixelRectToDipRect(PixelRect pixelRect, Vector dpi)
    {
        const double defaultDpi = 96d;
        double scaleX = defaultDpi / dpi.X;
        double scaleY = defaultDpi / dpi.Y;

        return new Rect(
            pixelRect.X * scaleX,
            pixelRect.Y * scaleY,
            pixelRect.Width * scaleX,
            pixelRect.Height * scaleY);
    }

    private static void ValidateSourceRect(PixelSize sourceSize, PixelRect sourceRect)
    {
        if ((sourceRect.Width <= 0)
            || (sourceRect.Height <= 0)
            || (sourceRect.X < 0)
            || (sourceRect.Y < 0)
            || (sourceRect.Right > sourceSize.Width)
            || (sourceRect.Bottom > sourceSize.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRect),
                sourceRect,
                "The crop rectangle must be inside the source bitmap.");
        }
    }
}

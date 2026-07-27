using Avalonia;

namespace Pica.Desktop;

internal static class Program
{
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const long GpuResourceCacheSizeBytes = 256L * BytesPerMegabyte;

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = GpuResourceCacheSizeBytes
            });
    }
}

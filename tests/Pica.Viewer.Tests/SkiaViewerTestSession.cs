using Avalonia;
using Avalonia.Headless;

namespace Pica.Viewer.Tests;

internal static class SkiaViewerTestSession
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<ViewerTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

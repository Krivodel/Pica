using Avalonia;
using Velopack;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;

namespace Pica.Desktop;

internal static class Program
{
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const long GpuResourceCacheSizeBytes =
        256L * BytesPerMegabyte;

    private static Exception? BackgroundActivationForwardingException;

    [STAThread]
    public static async Task Main(string[] args)
    {
        PicaLaunchContext launchContext = new(
            WindowsForegroundWindowCapture.Capture());
        VelopackApp.Build().Run();
        PicaBackgroundActivationClient activationClient = new();

        try
        {
            if (PicaBackgroundActivationRouting
                    .RunsBeforeFrameworkInitialization
                && activationClient.CanForward(args))
            {
                await activationClient
                    .ForwardAsync(
                        args,
                        launchContext.SourceWindowHandle,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                return;
            }
        }
        catch (Exception ex)
        {
            BackgroundActivationForwardingException = ex;
        }

        PicaDesktopApplicationRunner.Run(args, launchContext);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return BuildAvaloniaApp(PicaLaunchContext.Empty);
    }

    internal static AppBuilder BuildAvaloniaApp(
        PicaLaunchContext launchContext)
    {
        ArgumentNullException.ThrowIfNull(launchContext);

        return AppBuilder.Configure(() => new App(launchContext))
            .UsePlatformDetect()
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = GpuResourceCacheSizeBytes
            });
    }

    internal static Exception? TakeBackgroundActivationForwardingException()
    {
        return Interlocked.Exchange(
            ref BackgroundActivationForwardingException,
            null);
    }
}

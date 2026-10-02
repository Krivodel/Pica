using Avalonia;
using Velopack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;
using Pica.Desktop.Services.FileAssociations;
using Pica.Desktop.Services.Logging;

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
        VelopackApp velopack = VelopackApp.Build()
            .OnFirstRun(_ => launchContext = launchContext with { CanOfferFileAssociations = true });

        if (OperatingSystem.IsWindows())
        {
            velopack.OnAfterInstallFastCallback(_ => PicaInstallation.RecordFirstRun(PicaInstallation.ExecutablePath));
            velopack.OnBeforeUninstallFastCallback(_ =>
            {
                PicaClipboardShortcutRegistration.RemoveShortcuts();

                if (OperatingSystem.IsWindows())
                {
                    new WindowsFileAssociationStore().RemoveApplication();
                }
            });
        }

        velopack.Run();

        if (OperatingSystem.IsWindows() && PicaInstallation.HasFirstRunMarker)
        {
            launchContext = launchContext with { CanOfferFileAssociations = true };
        }

        if (OperatingSystem.IsWindows() && (args.Length == 1) && (args[0] == PicaLaunchArguments.ClipboardAgentArgument))
        {
            ServiceCollection services = new();
            services.AddPicaFileLogging();
            services.AddSingleton<IPicaDesktopStateService, PicaDesktopStateService>();
            services.AddSingleton<PicaClipboardAgent>();
            services.AddSingleton<PicaClipboardShortcutRegistration>();
            await using ServiceProvider provider = services.BuildServiceProvider();

            try
            {
                await provider.GetRequiredService<PicaClipboardAgent>().RunAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                provider.GetRequiredService<ILogger<PicaClipboardAgent>>().LogError(ex, "Pica clipboard agent stopped");
            }

            return;
        }

        PicaBackgroundActivationClient activationClient = new();

        if (OperatingSystem.IsWindows() && PicaLaunchArguments.IsClipboard(args)
            && (WindowsPicaActivationLocator.FindLastActive() is { } endpoint))
        {
            activationClient = new PicaBackgroundActivationClient(endpoint);
        }

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

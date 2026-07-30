using Microsoft.Extensions.DependencyInjection;

using SukiUI.Toasts;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;
using Pica.Desktop.Services.Updates;
using Pica.Desktop.Views.Updates;

namespace Pica.Desktop;

public static class DependencyInjection
{
    public static IServiceCollection AddPicaDesktop(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<PicaStartupRequestFactory>();
        services.AddSingleton<PicaDesktopViewerWindowFactory>();
        services.AddSingleton<PicaBackgroundIdleCoordinator>();
        services.AddSingleton<IPicaBackgroundIdleCoordinator>(provider =>
            provider.GetRequiredService<PicaBackgroundIdleCoordinator>());
        services.AddSingleton<
            IPicaIdleMemoryReclaimer,
            PicaIdleMemoryReclaimer>();
        services.AddSingleton<PicaBackgroundIdlePreparation>();
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<
            IApplicationUpdateService,
            VelopackApplicationUpdateService>();
        services.AddSingleton<ApplicationUpdateToastPresenter>();
        services.AddSingleton<ApplicationUpdateCoordinator>();
        services.AddSingleton<PicaApplicationLifecycle>();

        return services;
    }
}

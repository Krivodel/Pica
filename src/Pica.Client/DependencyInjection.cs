using Microsoft.Extensions.DependencyInjection;

namespace Pica.Client;

public static class DependencyInjection
{
    public static IServiceCollection AddPicaClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type markerType = typeof(IPicaExecutableSource);
        IEnumerable<Type> sourceTypes = typeof(DependencyInjection)
            .Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsPublic: true }
                && markerType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (Type sourceType in sourceTypes)
        {
            services.AddSingleton(markerType, sourceType);
        }

        services.AddSingleton<IPicaExecutableLocator, PicaExecutableLocator>();
        services.AddSingleton<IPicaProcessRunner, PicaProcessRunner>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Desktop.Services.Logging;

internal static class PicaLoggingDependencyInjection
{
    private const string LogsDirectoryName = "Logs";

    internal static IServiceCollection AddPicaFileLogging(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string logDirectoryPath = Path.Combine(
            localApplicationData,
            PicaProtocolConstants.ApplicationName,
            LogsDirectoryName);

        return services.AddPicaFileLogging(
            logDirectoryPath,
            PicaFileLoggingOptions.CreateDefault());
    }

    internal static IServiceCollection AddPicaFileLogging(
        this IServiceCollection services,
        string logDirectoryPath,
        PicaFileLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectoryPath);
        ArgumentNullException.ThrowIfNull(options);

        services.AddLogging(builder =>
            builder.SetMinimumLevel(options.MinimumLevel));
        services.AddSingleton<ILoggerProvider>(
            _ => new PicaFileLoggerProvider(
                logDirectoryPath,
                options));

        return services;
    }
}

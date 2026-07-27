using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Desktop.Services;
using Pica.Desktop.Views;
using Pica.Viewer;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Desktop;

public sealed partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private PicaHostConnection? _hostConnection;
    private ILogger<App>? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();
        AttachExceptionHandlers();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = StartAsync(desktopLifetime);
        }
        else
        {
            _logger?.LogWarning("Pica was started without a classic desktop application lifetime");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
        services.AddPicaViewer();
        services.AddSingleton<PicaStartupRequestFactory>();
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
    }

    private void AttachExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        try
        {
            PicaStartupRequestFactory startupRequestFactory = GetRequiredService<PicaStartupRequestFactory>();
            PicaStartupRequest startupRequest = await startupRequestFactory
                .CreateAsync(desktopLifetime.Args ?? [], CancellationToken.None);
            _hostConnection = startupRequest.HostConnection;
            IImageFormatRegistry formatRegistry = GetRequiredService<IImageFormatRegistry>();
            ILogger<ViewerActionDispatcher> actionLogger =
                GetRequiredService<ILogger<ViewerActionDispatcher>>();
            ViewerActionDispatcher actionDispatcher = new(
                _hostConnection,
                formatRegistry,
                actionLogger,
                startupRequest.ViewerRequest.ActionPayloadDirectory);
            IImageViewerWindowFactory windowFactory = GetRequiredService<IImageViewerWindowFactory>();
            ImageViewerWindow window = await windowFactory.CreateAsync(
                startupRequest.ViewerRequest,
                actionDispatcher,
                CancellationToken.None);

            ShowMainWindow(desktopLifetime, window);
            _logger?.LogInformation(
                "Pica viewer window opened with {ItemCount} images",
                startupRequest.ViewerRequest.Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pica failed to initialize");
            StartupErrorWindow errorWindow = new();
            ShowMainWindow(desktopLifetime, errorWindow);
        }
    }

    private void ShowMainWindow(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Window window)
    {
        window.Closed += OnMainWindowClosed;
        desktopLifetime.MainWindow = window;
        window.Show();
    }

    private TService GetRequiredService<TService>()
        where TService : notnull
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Pica service provider has not been created.");
        }

        return _serviceProvider.GetRequiredService<TService>();
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Pica main window closed; starting graceful shutdown");
        _ = e;

        if (_serviceProvider is not null)
        {
            IClipboardImageWriter clipboardImageWriter =
                _serviceProvider.GetRequiredService<IClipboardImageWriter>();
            await clipboardImageWriter.FlushAsync(CancellationToken.None);
        }

        if (_hostConnection is not null)
        {
            await _hostConnection.DisposeAsync();
            _hostConnection = null;
        }

        _serviceProvider?.Dispose();
        _serviceProvider = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            _logger?.LogInformation("Pica graceful shutdown completed");
            desktopLifetime.Shutdown();
        }
    }

    private void OnDispatcherUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _ = sender;
        _logger?.LogError(e.Exception, "Unhandled Pica UI-thread exception");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _ = sender;
        _logger?.LogError(e.Exception, "Unobserved Pica task exception");
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _ = sender;

        if (e.ExceptionObject is Exception ex)
        {
            _logger?.LogError(
                ex,
                "Unhandled Pica process exception. Terminating: {IsTerminating}",
                e.IsTerminating);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Desktop.Services;
using Pica.Desktop.Services.Background;
using Pica.Desktop.Services.Logging;
using Pica.Viewer;

namespace Pica.Desktop;

public sealed partial class App : Application
{
    private readonly PicaLaunchContext _launchContext;
    private ServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    public App()
        : this(PicaLaunchContext.Empty)
    {
    }

    internal App(PicaLaunchContext launchContext)
    {
        _launchContext = launchContext
            ?? throw new ArgumentNullException(nameof(launchContext));
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            StartClassicDesktopApplication(desktopLifetime);
        }
        else
        {
            ConfigureServices();
            AttachExceptionHandlers();
            _logger?.LogWarning(
                "Pica was started without a classic desktop application lifetime");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        ServiceCollection services = new();
        services.AddPicaFileLogging();
        services.AddPicaViewer();
        services.AddPicaDesktop();
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
    }

    private void LogForwardingFailure(Exception? forwardingException)
    {
        if (forwardingException is not null)
        {
            _logger?.LogWarning(
                forwardingException,
                "Pica could not forward startup arguments to a background process; continuing with a normal launch");
        }
    }

    private void StartClassicDesktopApplication(
        IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        Exception? forwardingException =
            Program.TakeBackgroundActivationForwardingException();

        if (!PicaBackgroundActivationRouting
                .RunsBeforeFrameworkInitialization)
        {
            PicaBackgroundActivationClient activationClient = new();

            try
            {
                if (activationClient.CanForward(
                        desktopLifetime.Args ?? Array.Empty<string>()))
                {
                    _ = ForwardBackgroundActivationAsync(
                        desktopLifetime,
                        activationClient,
                        _launchContext.SourceWindowHandle);
                    return;
                }
            }
            catch (Exception ex)
            {
                forwardingException = ex;
            }
        }

        StartApplicationLifecycle(
            desktopLifetime,
            forwardingException);
    }

    private void StartApplicationLifecycle(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Exception? forwardingException)
    {
        ConfigureServices();
        AttachExceptionHandlers();
        desktopLifetime.Exit += OnApplicationExit;
        LogForwardingFailure(forwardingException);
        PicaApplicationLifecycle lifecycle =
            GetRequiredService<PicaApplicationLifecycle>();
        _ = lifecycle.StartAsync(
            desktopLifetime,
            _launchContext.SourceWindowHandle,
            CancellationToken.None);
    }

    private async Task ForwardBackgroundActivationAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        PicaBackgroundActivationClient activationClient,
        long? sourceWindowHandle)
    {
        try
        {
            await activationClient.ForwardAsync(
                desktopLifetime.Args ?? Array.Empty<string>(),
                sourceWindowHandle,
                CancellationToken.None);
            desktopLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            StartApplicationLifecycle(desktopLifetime, ex);
        }
    }

    private void AttachExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledException +=
            OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException +=
            OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException +=
            OnDomainUnhandledException;
    }

    private void DetachExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledException -=
            OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -=
            OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -=
            OnDomainUnhandledException;
    }

    private TService GetRequiredService<TService>()
        where TService : notnull
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                "Pica service provider has not been created.");
        }

        return _serviceProvider.GetRequiredService<TService>();
    }

    private void OnApplicationExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs e)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.Exit -= OnApplicationExit;
        }

        _ = e;
        DetachExceptionHandlers();
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }

    private void OnDispatcherUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _ = sender;
        _logger?.LogError(
            e.Exception,
            "Unhandled Pica UI-thread exception");
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        _ = sender;
        _logger?.LogError(
            e.Exception,
            "Unobserved Pica task exception");
    }

    private void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
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

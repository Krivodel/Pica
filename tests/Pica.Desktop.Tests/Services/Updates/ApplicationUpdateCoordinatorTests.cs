using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using SukiUI.Controls;
using SukiUI.Toasts;
using Xunit;

using Pica.Desktop.Services.Updates;
using Pica.Desktop.Views.Updates;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services.Updates;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class ApplicationUpdateCoordinatorTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task StartMonitoring_AfterStopMonitoring_StartsForAnotherWindow()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ApplicationUpdateCoordinatorTests),
            SessionLock,
            () =>
            {
                SukiToastManager manager = new();
                ApplicationUpdateToastPresenter presenter = new(manager);
                ApplicationUpdateCoordinator coordinator = new(
                    new DisabledApplicationUpdateService(),
                    presenter,
                    NullLogger<ApplicationUpdateCoordinator>.Instance);
                SukiWindow firstWindow = new();
                SukiWindow secondWindow = new();
                coordinator.StartMonitoring(
                    firstWindow,
                    _ => Task.CompletedTask);
                coordinator.StopMonitoring();

                Action act = () => coordinator.StartMonitoring(
                    secondWindow,
                    _ => Task.CompletedTask);

                act.Should().NotThrow();
                coordinator.Dispose();
            });
    }

    private sealed class DisabledApplicationUpdateService :
        IApplicationUpdateService
    {
        public bool CanCheckForUpdates => false;

        public Task<PicaApplicationUpdate?> CheckForUpdateAsync(
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.FromResult<PicaApplicationUpdate?>(null);
        }

        public Task DownloadUpdateAsync(
            PicaApplicationUpdate update,
            IProgress<int> progress,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public void ApplyUpdateAndRestart(
            PicaApplicationUpdate update,
            IReadOnlyList<string> restartArguments)
        {
            throw new NotSupportedException();
        }

        public void ApplyUpdateAndExit(PicaApplicationUpdate update)
        {
            throw new NotSupportedException();
        }
    }
}

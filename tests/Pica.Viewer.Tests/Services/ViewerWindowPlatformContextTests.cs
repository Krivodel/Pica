using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerWindowPlatformContextTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task GetStorageProviderAsync_BeforeInitialization_CompletesAfterWindowBecomesAvailable()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerWindowPlatformContextTests),
            SessionLock,
            async () =>
            {
                ViewerWindowPlatformContext context = new();
                Task<IStorageProvider?> storageProviderTask =
                    context.GetStorageProviderAsync(
                        CancellationToken.None);

                storageProviderTask.IsCompleted.Should().BeFalse();

                Window window = new();
                context.Initialize(window);
                IStorageProvider? storageProvider =
                    await storageProviderTask;

                storageProvider.Should().BeSameAs(window.StorageProvider);
            });
    }
}

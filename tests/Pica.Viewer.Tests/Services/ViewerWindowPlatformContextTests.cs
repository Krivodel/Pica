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
    public async Task GetStorageProviderAsync_WithWindow_ReturnsWindowStorageProvider()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerWindowPlatformContextTests),
            SessionLock,
            async () =>
            {
                Window window = new();
                ViewerWindowPlatformContext context = new(window);

                IStorageProvider? storageProvider = await context
                    .GetStorageProviderAsync(CancellationToken.None);

                storageProvider.Should().BeSameAs(window.StorageProvider);
            });
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Viewer;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddPicaDesktop_WhenProviderBuilt_ResolvesApplicationLifecycle()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddPicaViewer();
        services.AddPicaDesktop();
        ServiceProviderOptions options = new()
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        };
        using ServiceProvider provider =
            services.BuildServiceProvider(options);

        PicaApplicationLifecycle lifecycle =
            provider.GetRequiredService<PicaApplicationLifecycle>();

        lifecycle.Should().NotBeNull();
    }

    [Fact]
    public void AddPicaViewer_WhenProviderBuilt_ResolvesWindowFactory()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddPicaViewer();
        ServiceProviderOptions options = new()
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        };
        using ServiceProvider provider =
            services.BuildServiceProvider(options);

        IImageViewerWindowFactory factory =
            provider.GetRequiredService<IImageViewerWindowFactory>();
        IWindowsExplorerItemOrderProvider explorerItemOrderProvider =
            provider.GetRequiredService<
                IWindowsExplorerItemOrderProvider>();

        factory.Should().NotBeNull();
        explorerItemOrderProvider.Should().NotBeNull();
        provider.GetServices<IViewerSettingContributionProvider>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void AddPicaDesktop_WhenProviderBuilt_RegistersDesktopSetting()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddPicaViewer();
        services.AddPicaDesktop();
        using ServiceProvider provider = services.BuildServiceProvider();

        IReadOnlyList<IViewerSettingContributionProvider> contributions =
            provider
                .GetServices<IViewerSettingContributionProvider>()
                .ToList();

        contributions.Should().ContainSingle()
            .Which.Should().BeOfType<
                PicaBackgroundIdleSettingContributionProvider>();
    }

    [Fact]
    public void AddPicaViewer_WithHostLoggingProvider_PreservesHostProvider()
    {
        ServiceCollection services = new();
        services.AddLogging(builder =>
            builder.AddProvider(NullLoggerProvider.Instance));
        services.AddPicaViewer();
        using ServiceProvider provider = services.BuildServiceProvider();

        IReadOnlyList<ILoggerProvider> loggerProviders = provider
            .GetServices<ILoggerProvider>()
            .ToList();

        loggerProviders.Should().ContainSingle()
            .Which.Should().BeSameAs(NullLoggerProvider.Instance);
    }
}

using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using Pica.Viewer;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests;

public sealed class DependencyInjectionTests
{
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

        factory.Should().NotBeNull();
    }
}

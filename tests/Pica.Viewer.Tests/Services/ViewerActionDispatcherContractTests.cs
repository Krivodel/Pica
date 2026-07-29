using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

public sealed class ViewerActionDispatcherContractTests
{
    [Fact]
    public async Task DispatchDerivedImageAsync_WithLegacyImplementation_UsesSelectionFallback()
    {
        LegacyViewerActionDispatcher implementation = new();
        IViewerActionDispatcher dispatcher = implementation;
        PicaActionDefinition action = new(
            "open",
            "Открыть",
            "M0,0",
            0d,
            PicaActionTargets.CurrentImage,
            0);
        PicaImageItem item = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "image.png",
            "image.png");
        byte[] pngContent = [1, 2, 3];

        await dispatcher.DispatchDerivedImageAsync(
            action,
            item,
            "image-R.png",
            pngContent,
            CancellationToken.None);

        implementation.SelectionDispatchCount.Should().Be(1);
        implementation.LastPngContent.Should().BeSameAs(pngContent);
    }
}

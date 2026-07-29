using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class ViewerActionDispatcherTests
{
    [Fact]
    public async Task DispatchDerivedImageAsync_WithPayloadDirectory_WritesPngPayload()
    {
        using PicaTemporaryDirectory directory = new();
        ViewerActionDispatcher dispatcher = new(
            null,
            new ImageFormatRegistry(),
            NullLogger<ViewerActionDispatcher>.Instance,
            directory.DirectoryPath);
        PicaActionDefinition action = new(
            "attach",
            "Прикрепить",
            "M0,0",
            0d,
            PicaActionTargets.CurrentImage,
            0);
        PicaImageItem item = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "image.png",
            "image.png");
        byte[] pngContent = [1, 2, 3, 4];

        await dispatcher.DispatchDerivedImageAsync(
            action,
            item,
            "image-R.png",
            pngContent,
            CancellationToken.None);

        string filePath = Directory
            .GetFiles(directory.DirectoryPath)
            .Should()
            .ContainSingle()
            .Subject;
        byte[] savedContent = await File.ReadAllBytesAsync(filePath);
        savedContent.Should().Equal(pngContent);
    }
}

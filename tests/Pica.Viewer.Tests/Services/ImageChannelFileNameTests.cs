using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageChannelFileNameTests
{
    [Fact]
    public void Create_WithJpegSource_UsesChannelSpecificPngName()
    {
        string fileName = ImageChannelFileName.Create(
            ImageChannel.Red,
            "photo.jpeg");

        fileName.Should().Be("photo-R.png");
    }
}

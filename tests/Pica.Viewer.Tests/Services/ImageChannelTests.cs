using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageChannelTests
{
    [Fact]
    public void CreateFileName_WithJpegSource_UsesChannelSpecificPngName()
    {
        string fileName = ImageChannel.Red.CreateFileName("photo.jpeg");

        fileName.Should().Be("photo-R.png");
    }
}

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Protocol;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaLaunchArgumentsTests
{
    [Fact]
    public void GetHostPipeName_WithHostedArguments_ReturnsPipeName()
    {
        string[] arguments =
            [PicaProtocolConstants.PipeArgument, "sample-pipe"];

        string? pipeName = PicaLaunchArguments.GetHostPipeName(arguments);

        pipeName.Should().Be("sample-pipe");
    }

    [Fact]
    public void GetHostPipeName_WithStandaloneArguments_ReturnsNull()
    {
        string[] arguments = [@"C:\Images\sample.png"];

        string? pipeName = PicaLaunchArguments.GetHostPipeName(arguments);

        pipeName.Should().BeNull();
    }
}

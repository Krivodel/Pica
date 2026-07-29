using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ClipboardImageWriterTests
{
    [Fact]
    public void Type_WithInjectedDependencies_DoesNotOwnTheirLifetime()
    {
        bool isDisposable = typeof(IDisposable)
            .IsAssignableFrom(typeof(ClipboardImageWriter));

        isDisposable.Should().BeFalse();
    }
}

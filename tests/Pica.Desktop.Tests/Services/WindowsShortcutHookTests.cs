using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Tests.Services.Background;

namespace Pica.Desktop.Tests.Services;

[Collection(WindowsShortcutCollection.Name)]
public sealed class WindowsShortcutHookTests
{
    [Fact]
    public async Task StartAsync_WhenStopped_CompletesMessageLoopAndReleasesHook()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using WindowsShortcutHook hook = new((_, _) => false);
        await hook.StartAsync(CancellationToken.None);
        hook.IsRunning.Should().BeTrue();

        await hook.DisposeAsync();

        hook.Completion.IsCompletedSuccessfully.Should().BeTrue();
        hook.IsRunning.Should().BeFalse();
    }
}

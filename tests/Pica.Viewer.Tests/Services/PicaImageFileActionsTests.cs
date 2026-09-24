using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class PicaImageFileActionsTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task SaveAsAsync_WithWebpSource_UsesOriginalFormatAndContent()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(PicaImageFileActionsTests),
            SessionLock,
            async () =>
            {
                using PicaTemporaryDirectory temporaryDirectory = new();
                string sourcePath = Path.Combine(
                    temporaryDirectory.DirectoryPath,
                    "source.webp");
                byte[] sourceContent = [10, 20, 30, 40];
                await File.WriteAllBytesAsync(sourcePath, sourceContent);
                using RecordingStorageProvider storageProvider = new();
                PicaImageFileActions actions = new(
                    new ImageFormatRegistry(),
                    new AvaloniaViewerUiDispatcher(),
                    new NullPlatformFileActions());
                actions.Attach(storageProvider.Provider);

                bool saved = await actions.SaveAsAsync(
                    sourcePath,
                    CancellationToken.None);

                saved.Should().BeTrue();
                storageProvider.SuggestedFileName.Should().Be("source.webp");
                storageProvider.SaveOptions?.FileTypeChoices?[0].Patterns
                    .Should().ContainSingle().Which.Should().Be("*.webp");
                storageProvider.SavePickerHasUiThreadAccess.Should().BeTrue();
                storageProvider.Destination.Content.Should().Equal(sourceContent);
            });
    }
}

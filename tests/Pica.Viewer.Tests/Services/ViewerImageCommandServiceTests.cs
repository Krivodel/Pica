using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerImageCommandServiceTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task DispatchCurrentAsync_WithSelectedChannel_DispatchesDerivedPng()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync();
                PicaActionDefinition action = new(
                    "attach",
                    "Прикрепить",
                    "M0,0",
                    0d,
                    PicaActionTargets.CurrentImage,
                    0);

                await context.CommandService.DispatchCurrentAsync(
                    action,
                    CancellationToken.None);

                context.ActionDispatcher.DerivedImageDispatchCount.Should().Be(1);
                context.ActionDispatcher.LastFileName.Should().Be("image-R.png");
                context.ActionDispatcher.LastPngContent.Should().NotBeEmpty();
                context.Readiness.WaitCount.Should().Be(1);
            });
    }

    [Fact]
    public async Task CopyCurrentAsync_WithSelectedChannel_CopiesPreparedChannelImage()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync();

                await context.CommandService.CopyCurrentAsync(
                    CancellationToken.None);

                context.ClipboardWriter.PreparedImageCount.Should().Be(1);
                context.ClipboardWriter.LastPreparedImage
                    .Should()
                    .NotBeNull();
                context.ClipboardWriter.FileCount.Should().Be(0);
                context.ClipboardWriter.FileWithImageCount.Should().Be(0);
                context.Readiness.WaitCount.Should().Be(1);
            });
    }

    [Fact]
    public async Task CopyCurrentAsync_WhenPresentationUnavailable_DoesNotCopy()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync();
                context.Readiness.IsReady = false;

                await context.CommandService.CopyCurrentAsync(
                    CancellationToken.None);

                context.Readiness.WaitCount.Should().Be(1);
                context.ClipboardWriter.PreparedImageCount.Should().Be(0);
                context.ClipboardWriter.FileCount.Should().Be(0);
                context.ClipboardWriter.FileWithImageCount.Should().Be(0);
            });
    }

    [Fact]
    public async Task PrepareCurrentOpenWithFileAsync_WithSelectedChannel_CreatesChannelPng()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync();

                await context.CommandService.PrepareCurrentOpenWithFileAsync(
                    CancellationToken.None);

                string filePath = context.CommandService.PreparedOpenWithFilePath
                    ?? throw new InvalidOperationException(
                        "The channel file path must be prepared.");
                Path.GetFileName(filePath)
                    .Should()
                    .StartWith("Pica-channel-R-");
                Path.GetExtension(filePath).Should().Be(".png");
                File.Exists(filePath).Should().BeTrue();
                File.ReadAllBytes(filePath).Should().NotBeEmpty();
                context.Readiness.WaitCount.Should().Be(1);
            });
    }

    [Fact]
    public async Task SaveCurrentAsync_WithSelectedChannel_SavesNamedChannelPng()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using RecordingStorageProvider storageProvider = new();
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync(
                        storageProvider.Provider);

                await context.CommandService.SaveCurrentAsync(
                    CancellationToken.None);

                storageProvider.SuggestedFileName.Should().Be("image-R.png");
                storageProvider.Destination.Content.Should().NotBeEmpty();
                storageProvider.Destination.Content
                    .Take(8)
                    .Should()
                    .Equal(137, 80, 78, 71, 13, 10, 26, 10);
                context.Readiness.WaitCount.Should().Be(1);
            });
    }

    [Fact]
    public async Task PrepareSelectionAsync_WithValidPixels_ReturnsPreparedCrop()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerImageCommandServiceTests),
            SessionLock,
            async () =>
            {
                using ViewerImageCommandTestContext context =
                    await ViewerImageCommandTestContext.CreateAsync();
                ImagePixelSelection selection = new(0, 0, 1, 1);

                PreparedClipboardImage? image =
                    await context.CommandService.PrepareSelectionAsync(
                        selection,
                        CancellationToken.None);

                image.Should().NotBeNull();
                image?.Dimensions.Should().Be(
                    new ImageDimensions(1, 1));
                image?.PngContent.Should().NotBeEmpty();
            });
    }
}

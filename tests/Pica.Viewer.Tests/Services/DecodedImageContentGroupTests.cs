using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class DecodedImageContentGroupTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(5d);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(
                new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task LoadAsync_WhenWaitingCallerCancels_KeepsSharedLoadRunning()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageContentGroupTests),
            SessionLock,
            async () =>
            {
                TaskCompletionSource<DecodedImage> completion = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken loadingToken = default;
                DecodedImageContentGroup group = new(
                    new ImageContentGroupDefinition(
                        ImageContentGroupKind.Animation,
                        2),
                    ct =>
                    {
                        loadingToken = ct;

                        return completion.Task;
                    });
                using CancellationTokenSource callerCancellation = new();
                Task<DecodedImage> firstWait = group.LoadAsync(
                    callerCancellation.Token);

                callerCancellation.Cancel();

                Func<Task> cancelledWait = async () =>
                    await firstWait;
                await cancelledWait.Should()
                    .ThrowAsync<OperationCanceledException>();
                loadingToken.IsCancellationRequested.Should().BeFalse();
                DecodedImage image = DecodedImage.CreateSingle(
                    BgraBitmapTestData.CreateBitmap());
                completion.SetResult(image);

                DecodedImage loadedImage = await group.LoadAsync(
                    CancellationToken.None);

                loadedImage.Should().BeSameAs(image);
                DisposeGroup(group);
            });
    }

    [Fact]
    public async Task CancelLoading_WhenContainerChanges_CancelsSharedLoad()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DecodedImageContentGroupTests),
            SessionLock,
            async () =>
            {
                TaskCompletionSource started = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                DecodedImageContentGroup group = new(
                    new ImageContentGroupDefinition(
                        ImageContentGroupKind.Animation,
                        2),
                    async ct =>
                    {
                        started.SetResult();
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            ct);

                        throw new InvalidOperationException(
                            "An infinite delay unexpectedly completed.");
                    });
                Task<DecodedImage> loadingTask = group.LoadAsync(
                    CancellationToken.None);
                using CancellationTokenSource timeout = new(TestTimeout);
                await started.Task.WaitAsync(timeout.Token);

                group.CancelLoading();

                Func<Task> cancelledLoad = async () =>
                    await loadingTask;
                await cancelledLoad.Should()
                    .ThrowAsync<OperationCanceledException>();
            });
    }

    [Fact]
    public async Task LoadAsync_WhenDecoderFails_ExposesFailure()
    {
        DecodedImageContentGroup group = new(
            new ImageContentGroupDefinition(
                ImageContentGroupKind.StillImages,
                2),
            _ => Task.FromException<DecodedImage>(
                new InvalidDataException("Damaged image group.")));

        Func<Task> load = async () =>
            await group.LoadAsync(CancellationToken.None);

        await load.Should().ThrowAsync<InvalidDataException>();
    }

    private static void DisposeGroup(DecodedImageContentGroup group)
    {
        foreach (DecodedImage image
            in group.StopAndGetLoadedImages())
        {
            image.Dispose();
        }
    }
}

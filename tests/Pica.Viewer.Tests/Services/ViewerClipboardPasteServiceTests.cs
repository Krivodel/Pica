using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerClipboardPasteServiceTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(1, false)]
    [InlineData(-1, true)]
    [InlineData(1, true)]
    public async Task PasteAsync_WithRepeatedPastes_FirstNavigationReturnsOriginal(int direction, bool contentNavigation)
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            byte[] bytes = await CreatePngAsync();
            string path = Path.Combine(directory.DirectoryPath, "original.png");
            await File.WriteAllBytesAsync(path, bytes);
            PicaImageItem original = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), path, "original.png");
            using ClipboardPasteTestContext context = new(new PicaImageItem[] { original });
            await context.LoadCoordinator.WaitForFullResolutionAsync(CancellationToken.None);
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("first.png", bytes) });

            await context.PasteService.PasteAsync(CancellationToken.None);
            Guid firstId = context.Presentation.CurrentItem?.Id ?? throw new InvalidOperationException("The first paste did not open.");
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("second.png", bytes) });
            await context.PasteService.PasteAsync(CancellationToken.None);
            (context.Presentation.CurrentItem?.Id).Should().NotBe(firstId);
            context.Session.SelectChannelImageMode();

            if (contentNavigation)
            {
                context.Session.NavigateContent(direction);
            }
            else
            {
                context.Session.Navigate(direction);
            }

            await context.LoadCoordinator.WaitForFullResolutionAsync(CancellationToken.None);

            context.Session.SelectedItem.Should().BeSameAs(original);
            context.Session.SelectedIndex.Should().Be(0);
            (context.Presentation.CurrentItem?.Id).Should().Be(original.Id);
            context.Presentation.IsCurrentImageFileBacked.Should().BeTrue();
            context.Session.IsMainImageModeActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PasteAsync_WhenRestoredFromOverlay_NextNavigationUsesOriginalList()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory directory = new();
            byte[] bytes = await CreatePngAsync();
            string firstPath = Path.Combine(directory.DirectoryPath, "first.png");
            string secondPath = Path.Combine(directory.DirectoryPath, "second.png");
            await File.WriteAllBytesAsync(firstPath, bytes);
            await File.WriteAllBytesAsync(secondPath, bytes);
            PicaImageItem first = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), firstPath, "first.png");
            PicaImageItem second = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), secondPath, "second.png");
            using ClipboardPasteTestContext context = new(new PicaImageItem[] { first, second });
            await context.LoadCoordinator.WaitForFullResolutionAsync(CancellationToken.None);
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("clipboard.png", bytes) });

            await context.PasteService.PasteAsync(CancellationToken.None);
            context.Session.Navigate(1);
            context.Session.SelectedItem.Should().BeSameAs(first);
            await context.LoadCoordinator.WaitForFullResolutionAsync(CancellationToken.None);
            context.Session.Navigate(1);
            await context.LoadCoordinator.WaitForFullResolutionAsync(CancellationToken.None);

            context.Session.Items.Should().Equal(first, second);
            context.Session.SelectedItem.Should().BeSameAs(second);
            context.Presentation.CurrentItem.Should().Be(second);
            context.Session.SelectedIndex.Should().Be(1);
        });
    }

    [Fact]
    public async Task PasteAsync_WithInvalidCandidateThenAnimation_OpensAnimationInEmptySession()
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            byte[] bytes = ApngImageTestData.GetContent();
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[]
                {
                    ClipboardImageInput.FromBytes("invalid.png", new byte[] { 1, 2, 3 }),
                    ClipboardImageInput.FromBytes("animation.png", bytes)
                });

            await context.PasteService.PasteAsync(CancellationToken.None);
            Guid? itemId = context.Presentation.CurrentItem?.Id;
            context.Session.Navigate(1);
            context.Session.NavigateFrame(1);

            context.Session.IsClipboardImageActive.Should().BeTrue();
            (context.Presentation.CurrentItem?.Id).Should().Be(itemId);
            context.Presentation.IsCurrentImageFileBacked.Should().BeFalse();
            context.Session.FrameCount.Should().BeGreaterThan(1);
            context.Session.SelectedFrameIndex.Should().Be(1);
            context.Session.SelectedIndex.Should().Be(-1);
        });
    }

    [Fact]
    public async Task PasteAsync_WithDamagedData_PreservesCurrentSourceAndReturnPoint()
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            byte[] bytes = await CreatePngAsync();
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("valid.png", bytes) });
            await context.PasteService.PasteAsync(CancellationToken.None);
            PicaImageItem? previous = context.Session.SelectedItem;
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("invalid.png", new byte[] { 1, 2, 3 }) });

            Func<Task> paste = () => context.PasteService.PasteAsync(CancellationToken.None);

            await paste.Should().ThrowAsync<InvalidDataException>();
            context.Session.SelectedItem.Should().BeSameAs(previous);
            context.Presentation.CurrentItem.Should().Be(previous);
        });
    }

    [Fact]
    public async Task PasteAsync_WhenNavigationCancelsUncooperativeReader_DiscardsItsCompletion()
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            byte[] bytes = await CreatePngAsync();
            TaskCompletionSource<IReadOnlyList<ClipboardImageInput>> captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Reader.Read = _ => captured.Task;

            Task paste = context.PasteService.PasteAsync(CancellationToken.None);
            context.Session.Navigate(-1);
            captured.SetResult(new ClipboardImageInput[] { ClipboardImageInput.FromBytes("late.png", bytes) });
            await paste;

            context.Session.IsClipboardImageActive.Should().BeFalse();
            context.Presentation.CurrentItem.Should().BeNull();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PasteAsync_WhenAnimationFrameNavigationCancelsPreparation_KeepsDisplayedAnimation(bool seekFrame)
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("animation.png", ApngImageTestData.GetContent()) });
            await context.PasteService.PasteAsync(CancellationToken.None);
            PicaImageItem? animation = context.Session.SelectedItem;
            TaskCompletionSource<IReadOnlyList<ClipboardImageInput>> captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Reader.Read = _ => captured.Task;

            Task paste = context.PasteService.PasteAsync(CancellationToken.None);

            if (seekFrame)
            {
                context.Session.SeekAnimationFrame(1);
            }
            else
            {
                context.Session.NavigateFrame(1);
            }

            captured.SetResult(new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(BgraBitmapTestData.CreateBitmap()) });
            await paste;

            context.Session.SelectedItem.Should().BeSameAs(animation);
            context.Session.SelectedFrameIndex.Should().Be(1);
            context.Session.IsClipboardImageActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PasteAsync_WhenSecondPasteCompletesBeforeFirst_KeepsSecondImage()
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            byte[] bytes = await CreatePngAsync();
            TaskCompletionSource<IReadOnlyList<ClipboardImageInput>> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Reader.Read = _ => first.Task;
            Task previousPaste = context.PasteService.PasteAsync(CancellationToken.None);
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("latest.png", bytes) });

            await context.PasteService.PasteAsync(CancellationToken.None);
            PicaImageItem? latest = context.Session.SelectedItem;
            first.SetResult(new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(BgraBitmapTestData.CreateBitmap()) });
            await previousPaste;

            context.Session.SelectedItem.Should().BeSameAs(latest);
            context.Presentation.CurrentItem.Should().Be(latest);
        });
    }

    [Fact]
    public async Task PasteAsync_WithBitmapLease_ReleasesReplacedImageAndImageAtClose()
    {
        await DispatchAsync(async () =>
        {
            byte[] bytes = await CreatePngAsync();
            using MemoryStream firstStream = new(bytes);
            using MemoryStream secondStream = new(bytes);
            TrackingBitmap first = new(firstStream);
            TrackingBitmap second = new(secondStream);
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(first) });

            await context.PasteService.PasteAsync(CancellationToken.None);
            first.IsDisposed.Should().BeFalse();
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(second) });
            await context.PasteService.PasteAsync(CancellationToken.None);
            context.Dispose();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5d));
            await first.WaitForDisposalAsync(timeout.Token);
            await second.WaitForDisposalAsync(timeout.Token);

            first.IsDisposed.Should().BeTrue();
            second.IsDisposed.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PasteAsync_WhenWindowClosesDuringCapture_DisposesLateBitmapWithoutApplyingIt()
    {
        await DispatchAsync(async () =>
        {
            byte[] bytes = await CreatePngAsync();
            using MemoryStream stream = new(bytes);
            TrackingBitmap bitmap = new(stream);
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            TaskCompletionSource<IReadOnlyList<ClipboardImageInput>> captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Reader.Read = _ => captured.Task;
            Task paste = context.PasteService.PasteAsync(CancellationToken.None);

            context.Dispose();
            captured.SetResult(new ClipboardImageInput[] { ClipboardImageInput.FromBitmap(bitmap) });
            await paste;

            bitmap.IsDisposed.Should().BeTrue();
            context.Session.IsClipboardImageActive.Should().BeFalse();
        });
    }

    [Fact]
    public async Task PasteAsync_WhenCanceledReaderFailsAfterNewPaste_IgnoresObsoleteFailure()
    {
        await DispatchAsync(async () =>
        {
            using ClipboardPasteTestContext context = new(Array.Empty<PicaImageItem>());
            TaskCompletionSource<IReadOnlyList<ClipboardImageInput>> previous = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Reader.Read = _ => previous.Task;
            Task obsolete = context.PasteService.PasteAsync(CancellationToken.None);
            byte[] bytes = await CreatePngAsync();
            context.Reader.Read = _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(
                new ClipboardImageInput[] { ClipboardImageInput.FromBytes("current.png", bytes) });

            await context.PasteService.PasteAsync(CancellationToken.None);
            PicaImageItem? current = context.Session.SelectedItem;
            previous.SetException(new IOException("An obsolete clipboard read failed."));
            await obsolete;

            context.Session.SelectedItem.Should().BeSameAs(current);
        });
    }

    private static async Task<byte[]> CreatePngAsync()
    {
        using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

        return await new PngImageEncoder().EncodeAsync(bitmap, CancellationToken.None);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerClipboardPasteServiceTests), SessionLock, action);
    }
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerClipboardFileCopyTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task CopyAsync_WithFileAndBitmap_UsesFileWithImageWriter()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ViewerClipboardFileCopyTests),
            SessionLock,
            async () =>
            {
                using RecordingStorageFile storageFile = new();
                RecordingViewerFilePickerService filePicker = new()
                {
                    SourceFile = storageFile.File
                };
                RecordingViewerClipboardWriter writer = new();
                ViewerClipboardFileCopy fileCopy = new(filePicker, writer);
                using Bitmap bitmap = BgraBitmapTestData.CreateBitmap();

                await fileCopy.CopyAsync("source.jpg", bitmap, CancellationToken.None);

                filePicker.RequestedFilePath.Should().Be("source.jpg");
                writer.FileWithImageCount.Should().Be(1);
                writer.LastFileWithImage.Should().BeSameAs(storageFile.File);
                writer.PreparedImageCount.Should().Be(0);
                writer.FileCount.Should().Be(0);
            });
    }

    [Fact]
    public async Task CopyAsync_WithoutBitmap_UsesFileWriter()
    {
        using RecordingStorageFile storageFile = new();
        RecordingViewerFilePickerService filePicker = new()
        {
            SourceFile = storageFile.File
        };
        RecordingViewerClipboardWriter writer = new();
        ViewerClipboardFileCopy fileCopy = new(filePicker, writer);

        await fileCopy.CopyAsync("source.jpg", null, CancellationToken.None);

        writer.FileCount.Should().Be(1);
        writer.FileWithImageCount.Should().Be(0);
        writer.PreparedImageCount.Should().Be(0);
    }

    [Fact]
    public async Task CopyAsync_WithoutStorageFile_ThrowsFileNotFoundException()
    {
        RecordingViewerFilePickerService filePicker = new();
        RecordingViewerClipboardWriter writer = new();
        ViewerClipboardFileCopy fileCopy = new(filePicker, writer);

        Func<Task> copy = () => fileCopy.CopyAsync(
            "missing.jpg",
            null,
            CancellationToken.None);

        await copy.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*missing.jpg*");
        writer.FileCount.Should().Be(0);
        writer.FileWithImageCount.Should().Be(0);
    }
}

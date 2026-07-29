using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;

namespace Pica.Viewer.Tests.Services;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class AvaloniaClipboardDataWriterTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task SetBytesAsync_FromWorkerThread_WritesOnUiThread()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(AvaloniaClipboardDataWriterTests),
            SessionLock,
            async () =>
            {
                RecordingClipboard clipboard = new();
                ViewerWindowPlatformContext platformContext = new(
                    null,
                    clipboard.Clipboard);
                using AvaloniaClipboardDataWriter writer = new(
                    platformContext,
                    NullLogger<AvaloniaClipboardDataWriter>.Instance);
                DataFormat<byte[]> format =
                    DataFormat.CreateBytesPlatformFormat(
                        PicaClipboardFormats.PngMime);
                await Task.Run(() => writer.SetBytesAsync(
                    format,
                    new byte[] { 1, 2, 3 },
                    CancellationToken.None));

                clipboard.SetDataHasUiThreadAccess.Should().BeTrue();
            });
    }
}

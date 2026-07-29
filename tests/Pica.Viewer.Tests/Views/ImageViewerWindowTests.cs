using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ImageViewerWindowTests
{
    private const int SourceImageWidth = 640;
    private const int SourceImageHeight = 480;
    private const int TestTimeoutSeconds = 10;

    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task Constructor_WithViewerContent_HostsCompleteView()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow();

            try
            {
                window.Show();

                window.Content.Should().BeOfType<ImageViewerView>();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Constructor_WithImageInformationSettings_PlacesResolutionAfterModificationDate()
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindow window = CreateWindow();

            try
            {
                window.Show();
                List<string> imageInformationSettings = window
                    .GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .Select(checkBox => checkBox.Content)
                    .OfType<string>()
                    .Where(content => content.StartsWith(
                        "Показывать",
                        StringComparison.Ordinal))
                    .ToList();

                imageInformationSettings.Should().Equal(
                    "Показывать название",
                    "Показывать формат",
                    "Показывать дату изменения",
                    "Показывать разрешение");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ImageSourceWorkflow_WithFastLoading_ReplacesPreviewWithFullResolutionAndSelectedChannel()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            await using (FileStream sourceStream = File.OpenRead(imagePath))
            using (SKManagedStream managedSource = new(sourceStream))
            using (SKCodec sourceCodec = SKCodec.Create(managedSource)
                ?? throw new InvalidDataException(
                    "The test image could not be decoded."))
            {
                sourceCodec.Info.Width.Should().Be(SourceImageWidth);
                sourceCodec.Info.Height.Should().Be(SourceImageHeight);
            }
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId,
                new List<PicaActionDefinition>(),
                null);
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = true
            };
            ViewerAnimationFrameScheduler animationFrameScheduler = new();
            TimeSpan frameTime = TimeSpan.Zero;
            int requestedFrameCount = 0;
            animationFrameScheduler.AnimationFrameRequested += (_, e) =>
            {
                requestedFrameCount++;
                frameTime += TimeSpan.FromMilliseconds(16);
                TimeSpan scheduledFrameTime = frameTime;
                DispatcherTimer.RunOnce(
                    () => e.FrameAction(scheduledFrameTime),
                    TimeSpan.FromMilliseconds(1));
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new ImageChannelBitmapLoader(
                    new ImageFormatRegistry()),
                animationFrameScheduler);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource<Bitmap> previewSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<Bitmap> fullResolutionSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<Bitmap> channelSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Bitmap? capturedPreviewSource = null;
            Bitmap? capturedFullResolutionSource = null;
            List<PixelSize> observedSourceSizes = [];
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property != Image.SourceProperty)
                    || (view.Image.Source is not Bitmap source))
                {
                    return;
                }

                observedSourceSizes.Add(source.PixelSize);

                if ((capturedPreviewSource is null)
                    && (source.PixelSize.Width
                        == ImagePreviewLoader.PreviewDecodeWidth))
                {
                    capturedPreviewSource = source;
                    previewSource.TrySetResult(source);
                    return;
                }

                if ((capturedPreviewSource is null)
                    || object.ReferenceEquals(
                        capturedPreviewSource,
                        source))
                {
                    return;
                }

                if (capturedFullResolutionSource is null)
                {
                    capturedFullResolutionSource = source;
                    fullResolutionSource.TrySetResult(source);
                    return;
                }

                if (!object.ReferenceEquals(
                    capturedFullResolutionSource,
                    source))
                {
                    channelSource.TrySetResult(source);
                }
            };

            try
            {
                window.Show();
                Bitmap preview = await previewSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));
                Bitmap fullResolution =
                    await fullResolutionSource.Task.WaitAsync(
                        TimeSpan.FromSeconds(TestTimeoutSeconds));

                window.KeyPress(
                    Key.Tab,
                    RawInputModifiers.None,
                    PhysicalKey.Tab,
                    null);
                Bitmap channel = await channelSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                preview.Should().NotBeSameAs(fullResolution);
                fullResolution.Should().NotBeSameAs(channel);
                view.Image.Source.Should().BeSameAs(channel);
                channel.PixelSize.Should().Be(fullResolution.PixelSize);
            }
            catch (TimeoutException ex)
            {
                string currentSourceSize = view.Image.Source is Bitmap source
                    ? source.PixelSize.ToString()
                    : "none";
                throw new InvalidOperationException(
                    $"The image source workflow stopped at {currentSourceSize}. "
                    + $"Preview: {previewSource.Task.IsCompleted}; "
                    + $"full resolution: {fullResolutionSource.Task.IsCompleted}; "
                    + $"channel: {channelSource.Task.IsCompleted}; "
                    + $"requested frames: {requestedFrameCount}; "
                    + $"observed sources: {string.Join(", ", observedSourceSizes)}.",
                    ex);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static PicaViewerRequest CreateEmptyRequest()
    {
        return new PicaViewerRequest(
            new List<PicaImageItem>(),
            Guid.Empty,
            new List<PicaActionDefinition>(),
            null);
    }

    private static ImageViewerWindow CreateWindow()
    {
        ImageViewerState state = new();

        return CreateWindow(
            CreateEmptyRequest(),
            state,
            new RecordingImageChannelBitmapLoader(),
            new ViewerAnimationFrameScheduler());
    }

    private static ImageViewerWindow CreateWindow(
        PicaViewerRequest request,
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader,
        ViewerAnimationFrameScheduler animationFrameScheduler)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        ArgumentNullException.ThrowIfNull(animationFrameScheduler);
        ImageFormatRegistry formatRegistry = new();
        AvaloniaViewerUiDispatcher uiDispatcher = new();
        ViewModelErrorHandler errorHandler = new(
            NullLogger<ViewModelErrorHandler>.Instance);
        ImageViewerPresentationFactory presentationFactory = new(
            new ImagePreviewLoader(
                formatRegistry,
                NullLogger<ImagePreviewLoader>.Instance),
            new FullResolutionImageLoader(formatRegistry),
            channelBitmapLoader,
            uiDispatcher,
            NullLogger<ImagePresentationController>.Instance,
            NullLogger<ImageLoadCoordinator>.Instance,
            NullLogger<ImagePreviewPrefetcher>.Instance);
        ImageViewerSettingsFactory settingsFactory = new(
            new RecordingImageViewerStateService(state),
            new ImageFileMetadataProvider(
                NullLogger<ImageFileMetadataProvider>.Instance),
            errorHandler);
        ClipboardImagePreparer clipboardImagePreparer = new();
        ClipboardFlushCoordinator flushCoordinator = new();
        ViewerClipboardFactory clipboardFactory = new(
            clipboardImagePreparer,
            flushCoordinator,
            NullLogger<AvaloniaClipboardDataWriter>.Instance);
        ImageViewerInteractionFactory interactionFactory = new(
            clipboardFactory,
            formatRegistry,
            uiDispatcher,
            new PngImageEncoder(),
            clipboardImagePreparer,
            new NullPlatformFileActions(),
            errorHandler,
            NullLogger<ImageViewerActionsViewModel>.Instance,
            NullLogger<ImageViewerOpenWithViewModel>.Instance,
            NullLogger<TemporaryImageFileStore>.Instance);
        ImageViewerWindowComposer composer = new(
            presentationFactory,
            settingsFactory,
            interactionFactory,
            NullLogger<ImageViewerWindow>.Instance,
            NullLogger<ImageViewerWindowLifetime>.Instance);

        return composer.Create(
            request,
            new RecordingViewerActionDispatcher(),
            state,
            animationFrameScheduler);
    }

    private static async Task<string> CreateImageAsync(
        string directoryPath)
    {
        string imagePath = Path.Combine(
            directoryPath,
            "image.png");
        SKImageInfo imageInfo = new(
            SourceImageWidth,
            SourceImageHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul);
        using SKBitmap bitmap = new(imageInfo);

        for (int y = 0; y < SourceImageHeight; y++)
        {
            for (int x = 0; x < SourceImageWidth; x++)
            {
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)((x + y) % 239),
                        (byte)(y % 241),
                        (byte)(x % 251),
                        byte.MaxValue));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData content = image.Encode(
            SKEncodedImageFormat.Png,
            100);
        await File.WriteAllBytesAsync(
            imagePath,
            content.ToArray());

        return imagePath;
    }

    private static async Task DispatchAsync(Action action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerWindowTests),
            SessionLock,
            action).ConfigureAwait(false);
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImageViewerWindowTests),
            SessionLock,
            action).ConfigureAwait(false);
    }
}

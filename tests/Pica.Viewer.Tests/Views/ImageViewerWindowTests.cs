using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
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
    public async Task Constructor_WithCheckerboardBackgroundEnabled_ShowsLayer()
    {
        await DispatchAsync(() =>
        {
            ImageViewerState state = new()
            {
                IsCheckerboardBackgroundEnabled = true
            };
            ImageViewerWindow window = CreateWindow(
                CreateEmptyRequest(),
                state,
                new RecordingImageChannelBitmapLoader());

            try
            {
                window.Show();
                ImageViewerView view = window.Content as ImageViewerView
                    ?? throw new InvalidOperationException(
                        "The viewer content must be created.");

                view.CheckerboardBackground.IsVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OnKeyDown_WithLoadedImage_TogglesCheckerboardLayerWithoutReplacingSource()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ImageViewerState state = new()
            {
                IsCheckerboardBackgroundEnabled = false
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new RecordingImageChannelBitmapLoader());
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource<Bitmap> loadedSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            view.Image.PropertyChanged += (_, e) =>
            {
                if ((e.Property == Image.SourceProperty)
                    && (view.Image.Source is Bitmap source))
                {
                    loadedSource.TrySetResult(source);
                }
            };

            try
            {
                window.Show();
                Bitmap source = await loadedSource.Task.WaitAsync(
                    TimeSpan.FromSeconds(TestTimeoutSeconds));

                window.KeyPress(
                    Key.T,
                    RawInputModifiers.None,
                    PhysicalKey.T,
                    null);

                view.CheckerboardBackground.IsVisible.Should().BeTrue();
                view.Image.Source.Should().BeSameAs(source);
                view.CheckerboardBackground.Width.Should().Be(
                    view.Image.Width);
                view.CheckerboardBackground.Height.Should().Be(
                    view.Image.Height);
                Canvas.GetLeft(view.CheckerboardBackground).Should().Be(
                    Canvas.GetLeft(view.Image));
                Canvas.GetTop(view.CheckerboardBackground).Should().Be(
                    Canvas.GetTop(view.Image));

                window.KeyPress(
                    Key.T,
                    RawInputModifiers.None,
                    PhysicalKey.T,
                    null);

                view.CheckerboardBackground.IsVisible.Should().BeFalse();
                view.Image.Source.Should().BeSameAs(source);
            }
            finally
            {
                window.Close();
            }
        });
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
    public async Task CreateAsync_WithoutActionsOrDispatcher_CreatesWindow()
    {
        await DispatchAsync(async () =>
        {
            IImageViewerWindowFactory factory = CreateWindowFactory(
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            PicaViewerRequest request = CreateEmptyRequest();

            ImageViewerWindow window = await factory.CreateAsync(
                request,
                CancellationToken.None);

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
    public async Task CreateAsync_WithActionsAndWithoutDispatcher_ThrowsArgumentException()
    {
        await DispatchAsync(async () =>
        {
            IImageViewerWindowFactory factory = CreateWindowFactory(
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader());
            PicaActionDefinition action = new(
                "test.action",
                "Test action",
                "M0,0 L1,1",
                0d,
                PicaActionTargets.CurrentImage,
                0);
            PicaViewerRequest request = new(
                new List<PicaImageItem>(),
                Guid.Empty,
                new List<PicaActionDefinition> { action });

            Func<Task> act = () => factory.CreateAsync(
                request,
                CancellationToken.None);

            await act.Should()
                .ThrowAsync<ArgumentException>()
                .WithParameterName(nameof(request));
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
                ItemId);
            ImageViewerState state = new()
            {
                IsFastLoadingEnabled = true
            };
            ImageViewerWindow window = CreateWindow(
                request,
                state,
                new ImageChannelBitmapLoader(
                    new ImageFormatRegistry()));
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
                    + $"observed sources: {string.Join(", ", observedSourceSizes)}.",
                    ex);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OnClosed_WithBlockedImageLoad_CompletesAfterBitmapDisposalAndVisualDetachment()
    {
        await DispatchAsync(async () =>
        {
            using PicaTemporaryDirectory temporaryDirectory = new();
            string imagePath = await CreateImageAsync(
                temporaryDirectory.DirectoryPath);
            PicaImageItem item = new(
                ItemId,
                imagePath,
                "image.png");
            PicaViewerRequest request = new(
                new List<PicaImageItem> { item },
                ItemId);
            ControlledFullResolutionImageLoader fullResolutionLoader = new(
                new List<string> { imagePath });
            ImageViewerWindow window = CreateWindow(
                request,
                new ImageViewerState(),
                new RecordingImageChannelBitmapLoader(),
                new ImagePreviewLoader(
                    new ImageFormatRegistry(),
                    NullLogger<ImagePreviewLoader>.Instance),
                fullResolutionLoader);
            ImageViewerView view = window.Content as ImageViewerView
                ?? throw new InvalidOperationException(
                    "The viewer content must be created.");
            TaskCompletionSource closed = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => closed.TrySetResult();
            using CancellationTokenSource timeout = new(
                TimeSpan.FromSeconds(TestTimeoutSeconds));
            window.Show();
            await fullResolutionLoader.WaitUntilStartedAsync(
                imagePath,
                timeout.Token);

            window.Close();
            await closed.Task.WaitAsync(timeout.Token);
            Task closeCleanupCompletion =
                window.CloseCleanupCompletion;

            fullResolutionLoader
                .GetCancellationToken(imagePath)
                .IsCancellationRequested.Should().BeTrue();
            closeCleanupCompletion.IsCompleted.Should().BeFalse();
            TrackingBitmap bitmap = new(imagePath);
            fullResolutionLoader.Complete(imagePath, bitmap);
            await closeCleanupCompletion.WaitAsync(timeout.Token);

            bitmap.IsDisposed.Should().BeTrue();
            view.Image.Source.Should().BeNull();
            view.DataContext.Should().BeNull();
            window.Content.Should().BeNull();
            window.LogoContent.Should().BeNull();
            window.RightWindowTitleBarControls.Should().BeEmpty();
            window.Hosts.Should().BeEmpty();
            window.Icon.Should().BeNull();
        });
    }

    [Fact]
    public async Task OnClosed_AfterCleanup_ReleasesWindowReference()
    {
        WeakReference? windowReference = null;
        await DispatchAsync(async () =>
        {
            windowReference =
                await CreateClosedWindowWeakReferenceAsync();
        });

        CollectGarbage();

        windowReference.Should().NotBeNull();
        windowReference.IsAlive.Should().BeFalse();
    }

    private static PicaViewerRequest CreateEmptyRequest()
    {
        return new PicaViewerRequest(
            new List<PicaImageItem>(),
            Guid.Empty);
    }

    private static ImageViewerWindow CreateWindow()
    {
        ImageViewerState state = new();

        return CreateWindow(
            CreateEmptyRequest(),
            state,
            new RecordingImageChannelBitmapLoader());
    }

    private static ImageViewerWindow CreateWindow(
        PicaViewerRequest request,
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader)
    {
        ImageFormatRegistry formatRegistry = new();

        return CreateWindow(
            request,
            state,
            channelBitmapLoader,
            new ImagePreviewLoader(
                formatRegistry,
                NullLogger<ImagePreviewLoader>.Instance),
            new FullResolutionImageLoader(formatRegistry));
    }

    private static ImageViewerWindow CreateWindow(
        PicaViewerRequest request,
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader,
        IImagePreviewLoader previewLoader,
        IFullResolutionImageLoader fullResolutionLoader)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        ArgumentNullException.ThrowIfNull(previewLoader);
        ArgumentNullException.ThrowIfNull(fullResolutionLoader);
        RecordingImageViewerStateService stateService = new(state);
        AvaloniaViewerUiDispatcher uiDispatcher = new();
        ImageViewerWindowComposer composer = CreateWindowComposer(
            stateService,
            uiDispatcher,
            channelBitmapLoader,
            previewLoader,
            fullResolutionLoader);

        return composer.Create(
            request,
            new RecordingViewerActionDispatcher(),
            state);
    }

    private static IImageViewerWindowFactory CreateWindowFactory(
        ImageViewerState state,
        IImageChannelBitmapLoader channelBitmapLoader)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        RecordingImageViewerStateService stateService = new(state);
        AvaloniaViewerUiDispatcher uiDispatcher = new();
        ImageViewerWindowComposer composer = CreateWindowComposer(
            stateService,
            uiDispatcher,
            channelBitmapLoader);

        return new ImageViewerWindowFactory(
            stateService,
            uiDispatcher,
            composer);
    }

    private static ImageViewerWindowComposer CreateWindowComposer(
        IImageViewerStateService stateService,
        IViewerUiDispatcher uiDispatcher,
        IImageChannelBitmapLoader channelBitmapLoader,
        IImagePreviewLoader? previewLoader = null,
        IFullResolutionImageLoader? fullResolutionLoader = null)
    {
        ArgumentNullException.ThrowIfNull(stateService);
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(channelBitmapLoader);
        ImageFormatRegistry formatRegistry = new();
        previewLoader ??= new ImagePreviewLoader(
            formatRegistry,
            NullLogger<ImagePreviewLoader>.Instance);
        fullResolutionLoader ??=
            new FullResolutionImageLoader(formatRegistry);
        ViewModelErrorHandler errorHandler = new(
            NullLogger<ViewModelErrorHandler>.Instance);
        ImageViewerPresentationFactory presentationFactory = new(
            previewLoader,
            fullResolutionLoader,
            channelBitmapLoader,
            uiDispatcher,
            NullLogger<ImagePresentationController>.Instance,
            NullLogger<ImageLoadCoordinator>.Instance,
            NullLogger<ImagePreviewPrefetcher>.Instance);
        ImageViewerSettingsFactory settingsFactory = new(
            stateService,
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

        return composer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference>
        CreateClosedWindowWeakReferenceAsync()
    {
        ImageViewerWindow window = CreateWindow();
        TaskCompletionSource closed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();

        window.Close();
        await closed.Task.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));
        await window.CloseCleanupCompletion.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));

        return new WeakReference(window);
    }

    private static void CollectGarbage()
    {
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            true,
            true);
        GC.WaitForPendingFinalizers();
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            true,
            true);
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

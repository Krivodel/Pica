using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageViewerStateServiceTests
{
    [Fact]
    public async Task LoadAsync_WithoutSavedState_EnablesCheckerboardBackgroundByDefault()
    {
        using ImageViewerStateTestContext context = new();

        ImageViewerState state = await context.Service.LoadAsync(
            CancellationToken.None);

        state.IsCheckerboardBackgroundEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithoutSavedState_UsesSixtySecondBackgroundIdleTimeout()
    {
        using ImageViewerStateTestContext context = new();

        ImageViewerState state = await context.Service.LoadAsync(
            CancellationToken.None);

        state.BackgroundIdleTimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public async Task SaveAsync_WithCheckerboardBackgroundDisabled_RoundTripsDisabledState()
    {
        using ImageViewerStateTestContext context = new();
        ImageViewerState state = new()
        {
            IsCheckerboardBackgroundEnabled = false
        };

        await context.Service.SaveAsync(state, CancellationToken.None);
        ImageViewerStateService reader = context.CreateService();
        ImageViewerState restoredState = await reader.LoadAsync(
            CancellationToken.None);

        restoredState.IsCheckerboardBackgroundEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithWindowPlacement_RoundTripsState()
    {
        using ImageViewerStateTestContext context = new();
        ImageViewerState state = ImageViewerStateTestFactory.CreateRememberedPlacementState();

        await context.Service.SaveAsync(state, CancellationToken.None);
        ImageViewerStateService reader = context.CreateService();

        ImageViewerState restoredState = await reader.LoadAsync(CancellationToken.None);

        restoredState.IsCheckerboardBackgroundEnabled.Should().BeTrue();
        restoredState.BackgroundIdleTimeoutSeconds.Should().Be(120);
        restoredState.IsFilteringEnabled.Should().BeFalse();
        restoredState.MovementSpeed.Should().Be(2);
        restoredState.ZoomSpeed.Should().Be(1);
        restoredState.ExpandOnDoubleClick.Should().BeFalse();
        restoredState.IsFastLoadingEnabled.Should().BeTrue();
        restoredState.AllowFreeZoomOut.Should().BeTrue();
        restoredState.IsPanningInertiaEnabled.Should().BeTrue();
        restoredState.ResizeBehavior.Should().Be(WindowResizeBehavior.FitWhenWindowed);
        restoredState.RememberWindowPlacement.Should().BeTrue();
        restoredState.ShowImageName.Should().BeFalse();
        restoredState.ShowImageFormat.Should().BeFalse();
        restoredState.ShowImageResolution.Should().BeFalse();
        restoredState.ShowImageModificationDate.Should().BeTrue();
        restoredState.IsWindowed.Should().BeTrue();
        restoredState.WindowX.Should().Be(-1200);
        restoredState.WindowY.Should().Be(80);
        restoredState.WindowWidth.Should().Be(900d);
        restoredState.WindowHeight.Should().Be(506.25d);
    }

    [Fact]
    public async Task SaveAsync_WhenWindowPlacementIsNotRemembered_ClearsPlacement()
    {
        using ImageViewerStateTestContext context = new();
        ImageViewerState state = new()
        {
            RememberWindowPlacement = false,
            IsWindowed = true,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 900d,
            WindowHeight = 600d
        };

        ImageViewerState restoredState = await SaveAndLoadAsync(context.Service, state);

        restoredState.RememberWindowPlacement.Should().BeFalse();
        restoredState.IsWindowed.Should().BeFalse();
        restoredState.WindowX.Should().BeNull();
        restoredState.WindowY.Should().BeNull();
        restoredState.WindowWidth.Should().BeNull();
        restoredState.WindowHeight.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithLegacyWindowPlacement_RestoresWindowedMode()
    {
        using ImageViewerStateTestContext context = new();
        const string legacyStateJson = """
            {
              "rememberWindowPlacement": true,
              "windowX": 120,
              "windowY": 80,
              "windowWidth": 900,
              "windowHeight": 600
            }
            """;
        await File.WriteAllTextAsync(
            context.StateFilePath,
            legacyStateJson,
            CancellationToken.None);

        ImageViewerState restoredState = await context.Service.LoadAsync(CancellationToken.None);

        restoredState.IsWindowed.Should().BeTrue();
        restoredState.IsCheckerboardBackgroundEnabled.Should().BeTrue();
        restoredState.IsFilteringEnabled.Should().BeTrue();
        restoredState.IsFastLoadingEnabled.Should().BeFalse();
        restoredState.AllowFreeZoomOut.Should().BeFalse();
        restoredState.IsPanningInertiaEnabled.Should().BeTrue();
        restoredState.ShowImageName.Should().BeFalse();
        restoredState.ShowImageFormat.Should().BeTrue();
        restoredState.ShowImageResolution.Should().BeTrue();
        restoredState.ShowImageModificationDate.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task LoadAsync_WithOutOfRangeBackgroundIdleTimeout_UsesDefault(
        int timeoutSeconds)
    {
        using ImageViewerStateTestContext context = new();
        string stateJson = $$"""
            {
              "backgroundIdleTimeoutSeconds": {{timeoutSeconds}}
            }
            """;
        await File.WriteAllTextAsync(
            context.StateFilePath,
            stateJson,
            CancellationToken.None);

        ImageViewerState restoredState = await context.Service.LoadAsync(
            CancellationToken.None);

        restoredState.BackgroundIdleTimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public async Task LoadAsync_WithLegacyDisabledSmoothPanning_PreservesInertia()
    {
        using ImageViewerStateTestContext context = new();
        const string legacyStateJson = """
            {
              "isSmoothPanningEnabled": false,
              "isPanningInertiaEnabled": true
            }
            """;
        await File.WriteAllTextAsync(
            context.StateFilePath,
            legacyStateJson,
            CancellationToken.None);

        ImageViewerState restoredState = await context.Service.LoadAsync(
            CancellationToken.None);

        restoredState.IsPanningInertiaEnabled.Should().BeTrue();
    }

    private static ImageViewerStateService CreateService(string stateFilePath)
    {
        return new ImageViewerStateService(
            stateFilePath,
            NullLogger<ImageViewerStateService>.Instance);
    }

    private static async Task<ImageViewerState> SaveAndLoadAsync(
        ImageViewerStateService service,
        ImageViewerState state)
    {
        await service.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

        return await service.LoadAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private sealed class ImageViewerStateTestContext : IDisposable
    {
        public string StateFilePath { get; }
        public ImageViewerStateService Service { get; }

        private readonly PicaTemporaryDirectory _temporaryDirectory;

        public ImageViewerStateTestContext()
        {
            _temporaryDirectory = new PicaTemporaryDirectory();
            StateFilePath = Path.Combine(
                _temporaryDirectory.DirectoryPath,
                "image-viewer.json");
            Service = ImageViewerStateServiceTests.CreateService(StateFilePath);
        }

        public ImageViewerStateService CreateService()
        {
            return ImageViewerStateServiceTests.CreateService(StateFilePath);
        }

        public void Dispose()
        {
            _temporaryDirectory.Dispose();
        }
    }
}

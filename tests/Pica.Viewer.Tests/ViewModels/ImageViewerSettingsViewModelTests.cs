using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerSettingsViewModelTests
{
    [Fact]
    public void Constructor_WithInitialState_ExposesState()
    {
        ImageViewerState state = CreateState();
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            state,
            out _,
            out _,
            out _);

        viewModel.MovementSpeed.Should().Be(2);
        viewModel.ZoomSpeed.Should().Be(4);
        viewModel.ExpandOnDoubleClick.Should().BeFalse();
        viewModel.IsFastLoadingEnabled.Should().BeFalse();
        viewModel.AllowFreeZoomOut.Should().BeTrue();
        viewModel.IsPanningInertiaEnabled.Should().BeFalse();
        viewModel.ResizeBehavior.Should().Be(
            WindowResizeBehavior.AlwaysFitImage);
        viewModel.RememberWindowPlacement.Should().BeTrue();
        viewModel.ShowImageName.Should().BeFalse();
        viewModel.ShowImageFormat.Should().BeFalse();
        viewModel.ShowImageResolution.Should().BeFalse();
        viewModel.ShowImageModificationDate.Should().BeFalse();
        viewModel.IsWindowed.Should().BeTrue();
        viewModel.WindowX.Should().Be(10);
        viewModel.WindowY.Should().Be(20);
        viewModel.WindowWidth.Should().Be(800d);
        viewModel.WindowHeight.Should().Be(600d);
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.HasErrorMessage.Should().BeFalse();

        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeMovementSpeedCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeMovementSpeedCommand.ExecuteAsync(3);

        viewModel.MovementSpeed.Should().Be(3);
        stateService.LastSavedState?.MovementSpeed.Should().Be(3);
        stateService.SaveCount.Should().Be(1);
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeZoomSpeedCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeZoomSpeedCommand.ExecuteAsync(1);

        viewModel.ZoomSpeed.Should().Be(1);
        stateService.LastSavedState?.ZoomSpeed.Should().Be(1);
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeExpandOnDoubleClickCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeExpandOnDoubleClickCommand.ExecuteAsync(true);

        viewModel.ExpandOnDoubleClick.Should().BeTrue();
        stateService.LastSavedState?.ExpandOnDoubleClick.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeFastLoadingCommand_WithValue_AppliesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out RecordingImageLoadingSettings loadingSettings,
            out _);

        await viewModel.ChangeFastLoadingCommand.ExecuteAsync(true);

        viewModel.IsFastLoadingEnabled.Should().BeTrue();
        loadingSettings.IsFastLoadingEnabled.Should().BeTrue();
        stateService.LastSavedState?.IsFastLoadingEnabled.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeAllowFreeZoomOutCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeAllowFreeZoomOutCommand.ExecuteAsync(false);

        viewModel.AllowFreeZoomOut.Should().BeFalse();
        stateService.LastSavedState?.AllowFreeZoomOut.Should().BeFalse();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangePanningInertiaCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangePanningInertiaCommand.ExecuteAsync(true);

        viewModel.IsPanningInertiaEnabled.Should().BeTrue();
        stateService.LastSavedState?.IsPanningInertiaEnabled.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeResizeBehaviorCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeResizeBehaviorCommand.ExecuteAsync(
            WindowResizeBehavior.Free);

        viewModel.ResizeBehavior.Should().Be(WindowResizeBehavior.Free);
        stateService.LastSavedState?.ResizeBehavior.Should().Be(
            WindowResizeBehavior.Free);
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeRememberWindowPlacementCommand_WhenEnabled_SavesCurrentPlacement()
    {
        ImageViewerState state = CreateState();
        state.RememberWindowPlacement = false;
        ViewerWindowPlacement placement = new(
            true,
            30,
            40,
            900d,
            700d);
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            state,
            placement,
            out RecordingImageViewerStateService stateService);

        await viewModel.ChangeRememberWindowPlacementCommand.ExecuteAsync(true);

        viewModel.RememberWindowPlacement.Should().BeTrue();
        stateService.LastSavedState.Should().BeEquivalentTo(
            new
            {
                IsWindowed = (bool?)true,
                WindowX = (int?)30,
                WindowY = (int?)40,
                WindowWidth = (double?)900d,
                WindowHeight = (double?)700d
            });
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeShowImageNameCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeShowImageNameCommand.ExecuteAsync(true);

        viewModel.ShowImageName.Should().BeTrue();
        stateService.LastSavedState?.ShowImageName.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeShowImageFormatCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeShowImageFormatCommand.ExecuteAsync(true);

        viewModel.ShowImageFormat.Should().BeTrue();
        stateService.LastSavedState?.ShowImageFormat.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeShowImageResolutionCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeShowImageResolutionCommand.ExecuteAsync(true);

        viewModel.ShowImageResolution.Should().BeTrue();
        stateService.LastSavedState?.ShowImageResolution.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeShowImageModificationDateCommand_WithValue_UpdatesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out _);

        await viewModel.ChangeShowImageModificationDateCommand.ExecuteAsync(
            true);

        viewModel.ShowImageModificationDate.Should().BeTrue();
        stateService.LastSavedState?.ShowImageModificationDate.Should().BeTrue();
        viewModel.Dispose();
    }

    [Fact]
    public async Task PersistWindowStateCommand_WithCurrentPlacement_SavesPlacement()
    {
        ViewerWindowPlacement placement = new(
            false,
            50,
            60,
            1000d,
            750d);
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            placement,
            out RecordingImageViewerStateService stateService);

        await viewModel.PersistWindowStateCommand.ExecuteAsync(null);

        stateService.LastSavedState.Should().BeEquivalentTo(
            new
            {
                IsWindowed = (bool?)false,
                WindowX = (int?)50,
                WindowY = (int?)60,
                WindowWidth = (double?)1000d,
                WindowHeight = (double?)750d
            });
        viewModel.Dispose();
    }

    [Fact]
    public async Task ToggleFilteringCommand_WhenEnabled_DisablesAndSavesState()
    {
        ImageViewerSettingsViewModel viewModel = CreateViewModel(
            CreateState(),
            out RecordingImageViewerStateService stateService,
            out _,
            out ImageViewerSession session);

        await viewModel.ToggleFilteringCommand.ExecuteAsync(null);

        viewModel.IsFilteringEnabled.Should().BeFalse();
        stateService.LastSavedState?.IsFilteringEnabled.Should().BeFalse();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeMovementSpeedCommand_WhenSaveFails_SetsSafeError()
    {
        ImageViewerSession session = CreateSession();
        RecordingImageLoadingSettings loadingSettings = new();
        ViewerWindowPlacementProvider placementProvider =
            CreatePlacementProvider(CreateState());
        RecordingViewModelErrorHandler errorHandler = new();
        ImageViewerSettingsViewModel viewModel = new(
            new ThrowingImageViewerStateService(),
            session,
            loadingSettings,
            placementProvider,
            errorHandler,
            CreateState());

        await viewModel.ChangeMovementSpeedCommand.ExecuteAsync(3);

        errorHandler.LastException.Should().Be(
            ThrowingImageViewerStateService.SaveException);
        errorHandler.LastOperationName.Should().Be(
            "ChangeMovementSpeedAsync");
        viewModel.ErrorMessage.Should().Be(
            RecordingViewModelErrorHandler.SafeMessage);
        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.Dispose();
    }

    [Fact]
    public async Task ChangeMovementSpeedCommand_WhileSaving_DisablesExecutingCommand()
    {
        BlockingImageViewerStateService stateService = new();
        ImageViewerSession session = CreateSession();
        RecordingImageLoadingSettings loadingSettings = new();
        ViewerWindowPlacementProvider placementProvider =
            CreatePlacementProvider(CreateState());
        RecordingViewModelErrorHandler errorHandler = new();
        ImageViewerSettingsViewModel viewModel = new(
            stateService,
            session,
            loadingSettings,
            placementProvider,
            errorHandler,
            CreateState());

        Task commandTask =
            viewModel.ChangeMovementSpeedCommand.ExecuteAsync(3);
        await stateService.SaveStarted;

        viewModel.IsLoading.Should().BeTrue();
        viewModel.ChangeMovementSpeedCommand.CanExecute(2).Should().BeFalse();

        stateService.CompleteSave();
        await commandTask;
        viewModel.Dispose();
    }

    private static ImageViewerState CreateState()
    {
        return new ImageViewerState
        {
            IsFilteringEnabled = true,
            MovementSpeed = 2,
            ZoomSpeed = 4,
            ExpandOnDoubleClick = false,
            IsFastLoadingEnabled = false,
            AllowFreeZoomOut = true,
            IsPanningInertiaEnabled = false,
            ResizeBehavior = WindowResizeBehavior.AlwaysFitImage,
            RememberWindowPlacement = true,
            ShowImageName = false,
            ShowImageFormat = false,
            ShowImageResolution = false,
            ShowImageModificationDate = false,
            IsWindowed = true,
            WindowX = 10,
            WindowY = 20,
            WindowWidth = 800d,
            WindowHeight = 600d
        };
    }

    private static ImageViewerSession CreateSession()
    {
        PicaViewerRequest request = new(
            new List<PicaImageItem>(),
            Guid.Empty,
            new List<PicaActionDefinition>(),
            null);

        return new ImageViewerSession(request, true);
    }

    private static ViewerWindowPlacementProvider CreatePlacementProvider(
        ImageViewerState state)
    {
        ViewerWindowPlacement placement = new(
            state.IsWindowed == true,
            state.WindowX,
            state.WindowY,
            state.WindowWidth,
            state.WindowHeight);

        return new ViewerWindowPlacementProvider(placement);
    }

    private static ImageViewerSettingsViewModel CreateViewModel(
        ImageViewerState state,
        out RecordingImageViewerStateService stateService,
        out RecordingImageLoadingSettings loadingSettings,
        out ImageViewerSession session)
    {
        ViewerWindowPlacement placement = new(
            state.IsWindowed == true,
            state.WindowX,
            state.WindowY,
            state.WindowWidth,
            state.WindowHeight);

        return CreateViewModel(
            state,
            placement,
            out stateService,
            out loadingSettings,
            out session);
    }

    private static ImageViewerSettingsViewModel CreateViewModel(
        ImageViewerState state,
        ViewerWindowPlacement placement,
        out RecordingImageViewerStateService stateService)
    {
        return CreateViewModel(
            state,
            placement,
            out stateService,
            out _,
            out _);
    }

    private static ImageViewerSettingsViewModel CreateViewModel(
        ImageViewerState state,
        ViewerWindowPlacement placement,
        out RecordingImageViewerStateService stateService,
        out RecordingImageLoadingSettings loadingSettings,
        out ImageViewerSession session)
    {
        stateService = new RecordingImageViewerStateService(state);
        loadingSettings = new RecordingImageLoadingSettings();
        session = CreateSession();
        ViewerWindowPlacementProvider placementProvider =
            new(placement);
        RecordingViewModelErrorHandler errorHandler = new();

        return new ImageViewerSettingsViewModel(
            stateService,
            session,
            loadingSettings,
            placementProvider,
            errorHandler,
            state);
    }
}

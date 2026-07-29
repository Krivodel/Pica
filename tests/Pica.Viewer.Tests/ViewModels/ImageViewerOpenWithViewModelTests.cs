using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerOpenWithViewModelTests
{
    [Fact]
    public void Constructor_WithSupportedPlatform_ExposesCapability()
    {
        RecordingPlatformFileActions platformActions = new()
        {
            SupportsOpenWith = true
        };
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions);

        viewModel.IsSupported.Should().BeTrue();
        viewModel.Applications.Should().BeEmpty();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task LoadApplicationsCommand_WithCurrentImage_ExposesApplications()
    {
        OpenWithApplication application = new(
            "viewer",
            "Viewer",
            null);
        RecordingPlatformFileActions platformActions = new()
        {
            Applications = new List<OpenWithApplication> { application }
        };
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions);

        await viewModel.LoadApplicationsCommand.ExecuteAsync(
            OpenWithTarget.CurrentImage);

        platformActions.LastFilePath.Should().Be(
            "C:\\Images\\image.png");
        viewModel.Applications.Should().Equal(application);
        viewModel.HasLoadedApplications.Should().BeTrue();
        viewModel.LoadedTarget.Should().Be(OpenWithTarget.CurrentImage);
    }

    [Fact]
    public async Task LoadApplicationsCommand_WithSelection_UsesSelectionFileName()
    {
        RecordingPlatformFileActions platformActions = new();
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions);

        await viewModel.LoadApplicationsCommand.ExecuteAsync(
            OpenWithTarget.Selection);

        platformActions.LastFilePath.Should().Be(
            PicaImageFormats.SelectionFileName);
        viewModel.HasLoadedApplications.Should().BeTrue();
        viewModel.LoadedTarget.Should().Be(OpenWithTarget.Selection);
    }

    [Fact]
    public async Task PrepareCurrentAndOpenWithCommands_WithApplication_OpensPreparedFile()
    {
        RecordingViewerImageCommandService imageCommands = new();
        RecordingPlatformFileActions platformActions = new();
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            imageCommands,
            platformActions);
        OpenWithApplication application = new(
            "viewer",
            "Viewer",
            null);

        await viewModel.PrepareCurrentImageCommand.ExecuteAsync(null);
        await viewModel.OpenWithApplicationCommand.ExecuteAsync(application);

        viewModel.IsPrepared.Should().BeTrue();
        imageCommands.PrepareCurrentOpenWithCount.Should().Be(1);
        platformActions.OpenWithCount.Should().Be(1);
        platformActions.LastFilePath.Should().Be(
            imageCommands.PreparedOpenWithFilePath);
        platformActions.LastApplication.Should().Be(application);
    }

    [Fact]
    public async Task PrepareSelectionAndChooseApplicationCommands_WithImage_ChoosesForPreparedFile()
    {
        RecordingViewerImageCommandService imageCommands = new();
        RecordingPlatformFileActions platformActions = new();
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            imageCommands,
            platformActions);
        PreparedClipboardImage image = new(
            new ImageDimensions(2, 1),
            8,
            new byte[8],
            new byte[] { 1, 2, 3 });

        await viewModel.PrepareSelectionCommand.ExecuteAsync(image);
        await viewModel.ChooseApplicationCommand.ExecuteAsync(null);

        viewModel.IsPrepared.Should().BeTrue();
        imageCommands.PrepareSelectionOpenWithCount.Should().Be(1);
        platformActions.ChooseApplicationCount.Should().Be(1);
        platformActions.LastFilePath.Should().Be(
            imageCommands.PreparedOpenWithFilePath);
    }

    [Fact]
    public async Task LoadApplicationsCommand_WhenPlatformFails_SetsSafeError()
    {
        InvalidOperationException exception = new("Platform failed.");
        RecordingPlatformFileActions platformActions = new()
        {
            ExceptionToThrow = exception
        };
        RecordingViewModelErrorHandler errorHandler = new();
        ImageViewerOpenWithViewModel viewModel = new(
            new RecordingViewerImageCommandService(),
            platformActions,
            errorHandler,
            NullLogger<ImageViewerOpenWithViewModel>.Instance);

        await viewModel.LoadApplicationsCommand.ExecuteAsync(
            OpenWithTarget.CurrentImage);

        errorHandler.LastException.Should().Be(exception);
        errorHandler.LastOperationName.Should().Be(
            "LoadApplicationsAsync");
        viewModel.ErrorMessage.Should().Be(
            RecordingViewModelErrorHandler.SafeMessage);
        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.HasLoadedApplications.Should().BeFalse();
        viewModel.LoadedTarget.Should().BeNull();
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadApplicationsCommand_WhileExecuting_DisablesActions()
    {
        RecordingPlatformFileActions platformActions = new()
        {
            BlockApplicationLoading = true
        };
        ImageViewerOpenWithViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions);

        Task commandTask =
            viewModel.LoadApplicationsCommand.ExecuteAsync(
                OpenWithTarget.CurrentImage);
        await platformActions.ApplicationLoadingStarted;

        viewModel.IsLoading.Should().BeTrue();
        viewModel.LoadApplicationsCommand
            .CanExecute(OpenWithTarget.Selection)
            .Should()
            .BeFalse();
        viewModel.PrepareCurrentImageCommand
            .CanExecute(null)
            .Should()
            .BeFalse();

        platformActions.CompleteApplicationLoading();
        await commandTask;

        viewModel.IsLoading.Should().BeFalse();
        viewModel.LoadApplicationsCommand
            .CanExecute(OpenWithTarget.Selection)
            .Should()
            .BeTrue();
    }

    private static ImageViewerOpenWithViewModel CreateViewModel(
        RecordingViewerImageCommandService imageCommands,
        RecordingPlatformFileActions platformActions)
    {
        return new ImageViewerOpenWithViewModel(
            imageCommands,
            platformActions,
            new RecordingViewModelErrorHandler(),
            NullLogger<ImageViewerOpenWithViewModel>.Instance);
    }
}

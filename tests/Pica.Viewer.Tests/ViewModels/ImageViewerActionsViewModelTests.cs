using System.ComponentModel;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerActionsViewModelTests
{
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_WithAvailableActions_ExposesInitialState()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.IsSaveTakingLong.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task CopyCurrentCommand_WithCurrentImage_DelegatesOperation()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        await viewModel.CopyCurrentCommand.ExecuteAsync(null);

        imageCommands.CopyCurrentCount.Should().Be(1);
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task DispatchCurrentCommand_WithAction_DelegatesOperation()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);
        PicaActionDefinition action = CreateAction();

        await viewModel.DispatchCurrentCommand.ExecuteAsync(action);

        imageCommands.DispatchCurrentCount.Should().Be(1);
        imageCommands.LastAction.Should().Be(action);
    }

    [Fact]
    public async Task SaveCurrentCommand_WithCurrentImage_DelegatesOperation()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        await viewModel.SaveCurrentCommand.ExecuteAsync(null);

        imageCommands.SaveCurrentCount.Should().Be(1);
        viewModel.IsSaveTakingLong.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCommand_WhenWritingTakesLong_ShowsAndClearsStatus(
        bool saveSelection)
    {
        RecordingViewerImageCommandService imageCommands = new()
        {
            BlockSaveCurrent = !saveSelection,
            BlockSaveSelection = saveSelection
        };
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);
        PreparedClipboardImage image = CreatePreparedImage();

        Task saveTask = saveSelection
            ? viewModel.SaveSelectionCommand.ExecuteAsync(image)
            : viewModel.SaveCurrentCommand.ExecuteAsync(null);
        await (saveSelection
            ? imageCommands.SaveSelectionStarted
            : imageCommands.SaveCurrentStarted);

        viewModel.IsSaveTakingLong.Should().BeFalse();
        await WaitForSaveIndicatorAsync(viewModel);
        viewModel.IsSaveTakingLong.Should().BeTrue();

        if (saveSelection)
        {
            imageCommands.CompleteSaveSelection();
        }
        else
        {
            imageCommands.CompleteSaveCurrent();
        }

        await saveTask;

        viewModel.IsSaveTakingLong.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCurrentCommand_WhenInterrupted_HidesSaveIndicator(
        bool cancel)
    {
        RecordingViewerImageCommandService imageCommands = new()
        {
            BlockSaveCurrent = true
        };
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        Task saveTask = viewModel.SaveCurrentCommand.ExecuteAsync(null);
        await imageCommands.SaveCurrentStarted;
        await WaitForSaveIndicatorAsync(viewModel);

        if (cancel)
        {
            viewModel.SaveCurrentCommand.Cancel();
        }
        else
        {
            imageCommands.FailSaveCurrent(new IOException("Save failed."));
        }

        await saveTask;

        viewModel.IsSaveTakingLong.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be(
            cancel ? null : RecordingViewModelErrorHandler.SafeMessage);
    }

    [Fact]
    public async Task SaveCurrentCommand_WhenWritingFinishesQuickly_NeverShowsStatus()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        await viewModel.SaveCurrentCommand.ExecuteAsync(null);
        await Task.Delay(TimeSpan.FromMilliseconds(1100d));

        viewModel.IsSaveTakingLong.Should().BeFalse();
    }

    [Fact]
    public async Task CopySelectionCommand_WithPreparedImage_DelegatesOperation()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);
        PreparedClipboardImage image = CreatePreparedImage();

        await viewModel.CopySelectionCommand.ExecuteAsync(image);

        imageCommands.CopySelectionCount.Should().Be(1);
        imageCommands.LastImage.Should().Be(image);
    }

    [Fact]
    public async Task DispatchSelectionCommand_WithPreparedAction_DelegatesOperation()
    {
        RecordingViewerImageCommandService imageCommands = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out PicaImageItem item);
        PicaActionDefinition action = CreateAction();
        PreparedClipboardImage image = CreatePreparedImage();
        PreparedSelectionAction selectionAction = new(
            action,
            item,
            image);

        await viewModel.DispatchSelectionCommand.ExecuteAsync(
            selectionAction);

        imageCommands.DispatchSelectionCount.Should().Be(1);
        imageCommands.LastAction.Should().Be(action);
        imageCommands.LastItem.Should().Be(item);
        imageCommands.LastImage.Should().Be(image);
    }

    [Fact]
    public async Task PrepareSelectionImageAsync_WhenServiceFails_SetsSafeError()
    {
        InvalidOperationException exception = new("Preparation failed.");
        RecordingViewerImageCommandService imageCommands = new()
        {
            PrepareSelectionException = exception
        };
        RecordingViewModelErrorHandler errorHandler = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            errorHandler,
            out _);
        ImagePixelSelection selection = new(0, 0, 1, 1);

        PreparedClipboardImage? image =
            await viewModel.PrepareSelectionImageAsync(
                selection,
                CancellationToken.None);

        image.Should().BeNull();
        errorHandler.LastException.Should().Be(exception);
        viewModel.ErrorMessage.Should().Be(
            RecordingViewModelErrorHandler.SafeMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveSelectionCommand_WithSaveOutcome_ExposesCompletion(
        bool wasSaved)
    {
        RecordingViewerImageCommandService imageCommands = new()
        {
            CompleteSelectionSave = wasSaved
        };
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);
        PreparedClipboardImage image = CreatePreparedImage();

        await viewModel.SaveSelectionCommand.ExecuteAsync(image);

        imageCommands.SaveSelectionCount.Should().Be(1);
        viewModel.WasSelectionSaved.Should().Be(wasSaved);
    }

    [Fact]
    public async Task RevealInFolderCommand_WithWindowMode_DelegatesCurrentPath()
    {
        RecordingPlatformFileActions platformActions = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions,
            out PicaImageItem item);

        await viewModel.RevealInFolderCommand.ExecuteAsync(
            FileRevealWindowMode.OpenNew);

        platformActions.RevealCount.Should().Be(1);
        platformActions.LastFilePath.Should().Be(item.FilePath);
        platformActions.LastWindowMode.Should().Be(
            FileRevealWindowMode.OpenNew);
    }

    [Fact]
    public async Task RevealInFolderCommand_WhenPlatformFails_SetsSafeError()
    {
        InvalidOperationException exception = new("Platform failed.");
        RecordingPlatformFileActions platformActions = new()
        {
            ExceptionToThrow = exception
        };
        RecordingViewModelErrorHandler errorHandler = new();
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            new RecordingViewerImageCommandService(),
            platformActions,
            errorHandler,
            out _);

        await viewModel.RevealInFolderCommand.ExecuteAsync(
            FileRevealWindowMode.ReuseExisting);

        errorHandler.LastException.Should().Be(exception);
        errorHandler.LastOperationName.Should().Be(
            "RevealInFolderAsync");
        viewModel.ErrorMessage.Should().Be(
            RecordingViewModelErrorHandler.SafeMessage);
        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task CopyCurrentCommand_WhileExecuting_DisablesActions()
    {
        RecordingViewerImageCommandService imageCommands = new()
        {
            BlockCopyCurrent = true
        };
        using ImageViewerActionsViewModel viewModel = CreateViewModel(
            imageCommands,
            new RecordingPlatformFileActions(),
            out _);

        Task commandTask =
            viewModel.CopyCurrentCommand.ExecuteAsync(null);
        await imageCommands.CopyCurrentStarted;

        viewModel.IsLoading.Should().BeTrue();
        viewModel.CopyCurrentCommand.CanExecute(null).Should().BeFalse();
        viewModel.SaveCurrentCommand.CanExecute(null).Should().BeFalse();

        imageCommands.CompleteCopyCurrent();
        await commandTask;

        viewModel.IsLoading.Should().BeFalse();
        viewModel.CopyCurrentCommand.CanExecute(null).Should().BeTrue();
    }

    private static async Task WaitForSaveIndicatorAsync(
        ImageViewerActionsViewModel viewModel)
    {
        if (!viewModel.IsSaveTakingLong)
        {
            TaskCompletionSource indicatorShown =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            PropertyChangedEventHandler handler = (_, eventArgs) =>
            {
                if ((eventArgs.PropertyName == nameof(viewModel.IsSaveTakingLong))
                    && viewModel.IsSaveTakingLong)
                {
                    indicatorShown.TrySetResult();
                }
            };
            viewModel.PropertyChanged += handler;

            try
            {
                if (!viewModel.IsSaveTakingLong)
                {
                    await indicatorShown.Task.WaitAsync(TimeSpan.FromSeconds(3d));
                }
            }
            finally
            {
                viewModel.PropertyChanged -= handler;
            }
        }
    }

    private static PicaActionDefinition CreateAction()
    {
        return new PicaActionDefinition(
            "open",
            "Открыть",
            "M0,0",
            0d,
            PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
            0);
    }

    private static PreparedClipboardImage CreatePreparedImage()
    {
        return new PreparedClipboardImage(
            new ImageDimensions(2, 1),
            8,
            new byte[8],
            new byte[] { 1, 2, 3 });
    }

    private static ImageViewerActionsViewModel CreateViewModel(
        RecordingViewerImageCommandService imageCommands,
        RecordingPlatformFileActions platformActions,
        out PicaImageItem item)
    {
        return CreateViewModel(
            imageCommands,
            platformActions,
            new RecordingViewModelErrorHandler(),
            out item);
    }

    private static ImageViewerActionsViewModel CreateViewModel(
        RecordingViewerImageCommandService imageCommands,
        RecordingPlatformFileActions platformActions,
        RecordingViewModelErrorHandler errorHandler,
        out PicaImageItem item)
    {
        item = new PicaImageItem(
            ItemId,
            "C:\\Images\\image.png",
            "image.png");
        PicaViewerRequest request = new(
            new List<PicaImageItem> { item },
            item.Id);
        ImageViewerSession session = new(request, true);
        StubImagePresentationInfo presentation = new();
        presentation.SetPresentation(
            item,
            new ImageDimensions(640, 480));

        return new ImageViewerActionsViewModel(
            imageCommands,
            presentation,
            session,
            platformActions,
            errorHandler,
            NullLogger<ImageViewerActionsViewModel>.Instance);
    }
}

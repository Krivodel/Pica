using FluentAssertions;
using Xunit;

using Pica.Desktop.Resources;
using Pica.Desktop.ViewModels;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Tests.ViewModels;

public sealed class FileAssociationsViewModelTests
{
    [Fact]
    public async Task FilterText_SearchAndClearSelection_PreservesHiddenChoicesUntilExplicitlyCleared()
    {
        FakeFileAssociationService service = new();
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.FilterText = "WEBP";

        viewModel.Formats.Where(format => format.MatchesFilter).Select(format => format.Extension).Should().Equal(".webp");
        viewModel.Formats.Single(format => format.Extension == ".png").IsSelected.Should().BeTrue();
        viewModel.SelectionSummary.Should().Be("Выбрано: 1 из 3");

        viewModel.SelectAllCommand.Execute(null);
        await viewModel.ApplyCommand.ExecuteAsync(null);

        service.Selected.Should().BeEquivalentTo(service.SupportedExtensions);
        viewModel.StatusMessage.Should().Be(DesktopUiStrings.FileAssociationsApplied);
        viewModel.CloseButtonText.Should().Be(DesktopUiStrings.Close);

        viewModel.ClearSelectionCommand.Execute(null);
        viewModel.FilterText = "unknown";

        viewModel.HasMatchingFormats.Should().BeFalse();
        viewModel.Formats.Should().OnlyContain(format => !format.IsSelected);
        viewModel.HasStatusMessage.Should().BeFalse();

        viewModel.FilterText = null;

        viewModel.Formats.Should().OnlyContain(format => format.MatchesFilter);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("apply")]
    public async Task Commands_ServiceFails_ShowsSafeErrorAndEndsLoading(string operation)
    {
        FakeFileAssociationService service = new();
        FileAssociationsViewModel viewModel = CreateViewModel(service);

        if (operation == "apply")
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }

        service.Failure = new IOException("Secret internal filesystem path");

        if (operation == "apply")
        {
            await viewModel.ApplyCommand.ExecuteAsync(null);
        }
        else
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }

        viewModel.ErrorMessage.Should().Be(DesktopUiStrings.FileAssociationsFailed);
        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ApplyCommand.CanExecute(null).Should().Be(operation == "apply");
    }

    [Fact]
    public async Task ApplyCommand_RestorationFails_ShowsDistinctError()
    {
        FakeFileAssociationService service = new();
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        service.Failure = new AggregateException(new IOException("Test restoration failure"));

        await viewModel.ApplyCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be(DesktopUiStrings.FileAssociationsRestoreFailed);
        viewModel.HasStatusMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyCommand_WhileBusy_DisablesChangesAndCancellationEndsOperation()
    {
        FakeFileAssociationService service = new();
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        TaskCompletionSource applying = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Applying = ct => applying.Task.WaitAsync(ct);

        Task operation = viewModel.ApplyCommand.ExecuteAsync(null);

        viewModel.IsLoading.Should().BeTrue();
        viewModel.ApplyCommand.CanExecute(null).Should().BeFalse();
        viewModel.CloseCommand.CanExecute(null).Should().BeFalse();
        viewModel.SelectAllCommand.CanExecute(null).Should().BeFalse();

        viewModel.CancelPendingOperations();
        await operation;

        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasErrorMessage.Should().BeFalse();
        viewModel.HasStatusMessage.Should().BeFalse();
    }

    private static FileAssociationsViewModel CreateViewModel(FakeFileAssociationService service)
    {
        return new FileAssociationsViewModel(service, new RecordingViewModelErrorHandler());
    }
}

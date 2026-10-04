using FluentAssertions;
using Xunit;

using Pica.Desktop.Resources;
using Pica.Desktop.ViewModels;
using Pica.Viewer.Services;
using Pica.Viewer.ViewModels;

namespace Pica.Desktop.Tests.ViewModels;

public sealed class FileAssociationsViewModelTests
{
    [Fact]
    public void FormatGroups_UnsortedExtensions_OrderGroupsAndShareSelectionWithTheCompleteFormatList()
    {
        ImageFormatRegistry registry = new();
        FakeFileAssociationService service = new()
        {
            SupportedExtensions = registry.GetSupportedExtensions().Reverse().ToArray()
        };

        FileAssociationsViewModel viewModel = CreateViewModel(service);

        FileAssociationFormatViewModel[] groupedFormats = viewModel.FormatGroups
            .SelectMany(group => group.Formats).ToArray();
        groupedFormats.Select(format => format.Extension).Should().BeEquivalentTo(service.SupportedExtensions);
        groupedFormats.Should().OnlyContain(format => viewModel.Formats.Contains(format));
        viewModel.FormatGroups.Select(group => group.Formats[0].Extension)
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        viewModel.FormatGroups.Should().OnlyContain(group => group.Formats.Select(format => format.Extension)
            .SequenceEqual(group.Formats.Select(format => format.Extension).Order(StringComparer.OrdinalIgnoreCase)));

        FileAssociationFormatGroupViewModel jpeg = viewModel.FormatGroups
            .Single(group => group.Formats.Any(format => string.Equals(format.Extension, ".jpg", StringComparison.OrdinalIgnoreCase)));
        jpeg.Formats.Select(format => format.Extension).Should().Equal(".jpeg", ".jpg");
        jpeg.Formats.Single(format => string.Equals(format.Extension, ".jpg", StringComparison.OrdinalIgnoreCase)).IsSelected = true;
        viewModel.Formats.Single(format => string.Equals(format.Extension, ".jpg", StringComparison.OrdinalIgnoreCase)).IsSelected.Should().BeTrue();
    }

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

        viewModel.Formats.Should().OnlyContain(format => format.IsSelected);

        viewModel.ClearSelectionCommand.Execute(null);
        viewModel.FilterText = "unknown";

        viewModel.HasMatchingFormats.Should().BeFalse();
        viewModel.Formats.Should().OnlyContain(format => !format.IsSelected);

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
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

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

        viewModel.ErrorMessage.Should().Be(DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsFailed));
        closed.Should().BeFalse();
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

        viewModel.ErrorMessage.Should().Be(DesktopLocalization.Get(PicaDesktopLocalizationKeys.FileAssociationsRestoreFailed));
    }

    [Fact]
    public async Task ApplyCommand_WhileBusy_DisablesChangesAndCancellationEndsOperation()
    {
        FakeFileAssociationService service = new();
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        TaskCompletionSource applying = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Applying = ct => applying.Task.WaitAsync(ct);
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        Task operation = viewModel.ApplyCommand.ExecuteAsync(null);

        viewModel.IsLoading.Should().BeTrue();
        viewModel.ApplyCommand.CanExecute(null).Should().BeFalse();
        viewModel.CloseCommand.CanExecute(null).Should().BeFalse();
        viewModel.SelectAllCommand.CanExecute(null).Should().BeFalse();

        viewModel.CancelPendingOperations();
        await operation;

        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasErrorMessage.Should().BeFalse();
        closed.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyCommand_Success_ClosesOnlyAfterSavingAndFinishingLoading()
    {
        TaskCompletionSource saving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeFileAssociationService service = new() { Applying = _ => saving.Task };
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectAllCommand.Execute(null);
        int closes = 0;
        viewModel.CloseRequested += (_, _) =>
        {
            viewModel.IsLoading.Should().BeFalse();
            service.Selected.Should().BeEquivalentTo(service.SupportedExtensions);
            closes++;
        };

        Task applying = viewModel.ApplyCommand.ExecuteAsync(null);

        closes.Should().Be(0);

        saving.SetResult();
        await applying;

        closes.Should().Be(1);
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_DismissalPending_ClosesOnlyAfterChoiceIsSaved()
    {
        TaskCompletionSource saving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeFileAssociationService service = new() { Dismissing = _ => saving.Task };
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        Task closing = viewModel.CloseCommand.ExecuteAsync(null);

        closed.Should().BeFalse();
        viewModel.IsLoading.Should().BeTrue();
        viewModel.ApplyCommand.CanExecute(null).Should().BeFalse();

        saving.SetResult();
        await closing;

        closed.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_DismissalFails_KeepsMenuOpenAndShowsSafeError()
    {
        FakeFileAssociationService service = new()
        {
            Dismissing = _ => Task.FromException(new IOException("Private settings path"))
        };
        FileAssociationsViewModel viewModel = CreateViewModel(service);
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        await viewModel.CloseCommand.ExecuteAsync(null);

        closed.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be(DesktopLocalization.Get(PicaDesktopLocalizationKeys.PreferenceChoiceFailed));
        viewModel.CloseCommand.CanExecute(null).Should().BeTrue();
    }

    private static FileAssociationsViewModel CreateViewModel(FakeFileAssociationService service)
    {
        return new FileAssociationsViewModel(service, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
    }
}

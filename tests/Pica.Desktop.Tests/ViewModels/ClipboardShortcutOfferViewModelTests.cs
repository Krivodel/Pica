using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Resources;
using Pica.Desktop.Services;
using Pica.Desktop.ViewModels;

namespace Pica.Desktop.Tests.ViewModels;

public sealed class ClipboardShortcutOfferViewModelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChooseCommand_Success_ClosesAfterSavingOnlyTheChosenAction(bool enable)
    {
        bool enabled = false;
        bool dismissed = false;
        bool closed = false;
        ClipboardShortcutOfferViewModel viewModel = new(_ =>
        {
            enabled = true;
            return Task.CompletedTask;
        }, _ =>
        {
            dismissed = true;
            return Task.CompletedTask;
        }, new RecordingViewModelErrorHandler());
        viewModel.CloseRequested += (_, _) =>
        {
            viewModel.IsLoading.Should().BeFalse();
            closed = true;
        };
        IAsyncRelayCommand command = enable ? viewModel.EnableCommand : viewModel.DeclineCommand;

        await command.ExecuteAsync(null);

        enabled.Should().Be(enable);
        dismissed.Should().Be(!enable);
        closed.Should().BeTrue();
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChooseCommand_SaveFails_KeepsOfferOpenAndLogsSafeError(bool enable)
    {
        IOException exception = new("Private settings path");
        RecordingViewModelErrorHandler errors = new();
        bool closed = false;
        ClipboardShortcutOfferViewModel viewModel = new(_ => Task.FromException(exception),
            _ => Task.FromException(exception), errors);
        viewModel.CloseRequested += (_, _) => closed = true;
        IAsyncRelayCommand command = enable ? viewModel.EnableCommand : viewModel.DeclineCommand;

        await command.ExecuteAsync(null);

        closed.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be(DesktopUiStrings.PreferenceChoiceFailed);
        viewModel.IsLoading.Should().BeFalse();
        errors.LastException.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task EnableCommand_OccupiedShortcut_ShowsSpecificError()
    {
        PicaShortcutException exception = new(PicaShortcutFailure.Occupied);
        ClipboardShortcutOfferViewModel viewModel = new(_ => Task.FromException(exception),
            _ => Task.CompletedTask, new RecordingViewModelErrorHandler());

        await viewModel.EnableCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be(PicaClipboardShortcutSettingContributionProvider.GetErrorMessage(exception));
    }

    [Fact]
    public async Task EnableCommand_CanceledWhileBusy_ReenablesActionsWithoutClosing()
    {
        bool closed = false;
        ClipboardShortcutOfferViewModel viewModel = new(ct => Task.Delay(Timeout.Infinite, ct),
            _ => Task.CompletedTask, new RecordingViewModelErrorHandler());
        viewModel.CloseRequested += (_, _) => closed = true;

        Task enabling = viewModel.EnableCommand.ExecuteAsync(null);

        viewModel.IsLoading.Should().BeTrue();
        viewModel.EnableCommand.CanExecute(null).Should().BeFalse();
        viewModel.DeclineCommand.CanExecute(null).Should().BeFalse();

        viewModel.EnableCommand.Cancel();
        await enabling;

        closed.Should().BeFalse();
        viewModel.HasErrorMessage.Should().BeFalse();
        viewModel.DeclineCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void IsRecording_WhileRecording_DisablesOfferActionsUntilRecordingEnds()
    {
        ClipboardShortcutOfferViewModel viewModel = new(_ => Task.CompletedTask,
            _ => Task.CompletedTask, new RecordingViewModelErrorHandler());

        viewModel.IsRecording = true;

        viewModel.EnableCommand.CanExecute(null).Should().BeFalse();
        viewModel.DeclineCommand.CanExecute(null).Should().BeFalse();

        viewModel.IsRecording = false;

        viewModel.EnableCommand.CanExecute(null).Should().BeTrue();
        viewModel.DeclineCommand.CanExecute(null).Should().BeTrue();
    }
}

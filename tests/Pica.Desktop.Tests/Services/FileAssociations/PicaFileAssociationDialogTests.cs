using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Services.FileAssociations;
using Pica.Desktop.Tests.ViewModels;
using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services.FileAssociations;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class PicaFileAssociationDialogTests
{
    private const int MaximumDialogWaitAttempts = 500;
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task ShowAsync_ClosedDuringApply_WaitsForCanceledOperationBeforeCompleting()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaFileAssociationDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FakeFileAssociationService service = new() { Applying = _ => completion.Task };
            PicaFileAssociationDialog dialog = new(service, stateService, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = dialog.ShowAsync(owner, CancellationToken.None);
                FileAssociationsWindow window = await WaitForDialogAsync(owner);
                FileAssociationsViewModel viewModel = window.DataContext as FileAssociationsViewModel
                    ?? throw new InvalidOperationException("Missing settings view model.");
                viewModel.SelectAllCommand.Execute(null);
                Task applying = viewModel.ApplyCommand.ExecuteAsync(null);

                window.Close();

                dialog.Completion.IsCompleted.Should().BeFalse();

                completion.SetResult();
                await applying;
                await showing;

                service.Selected.Should().Equal(".png");
                dialog.Completion.IsCompleted.Should().BeTrue();
            }
            finally
            {
                completion.TrySetResult();
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task ShowIfNeededAsync_SkippedOffer_DoesNotOfferAgainAndCanReopenFromSettings()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaFileAssociationDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            FakeFileAssociationStore store = new();
            PicaFileAssociationService service = new(new ImageFormatRegistry(), store, stateService);
            PicaFileAssociationDialog dialog = new(service, stateService, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
            Window owner = new();

            try
            {
                owner.Show();
                Task firstRun = dialog.ShowIfNeededAsync(owner, CancellationToken.None);
                FileAssociationsWindow offer = await WaitForDialogAsync(owner);
                FileAssociationsViewModel firstViewModel = offer.DataContext as FileAssociationsViewModel
                    ?? throw new InvalidOperationException("Missing offer view model.");

                firstViewModel.Formats.Should().OnlyContain(format => format.IsSelected);

                offer.Close();
                await firstRun;
                await dialog.ShowIfNeededAsync(owner, CancellationToken.None);

                owner.OwnedWindows.Should().BeEmpty();
                store.Writes.Should().BeEmpty();

                Task reopening = dialog.ShowAsync(owner, CancellationToken.None);
                FileAssociationsWindow reopened = await WaitForDialogAsync(owner);
                FileAssociationsViewModel settingsViewModel = reopened.DataContext as FileAssociationsViewModel
                    ?? throw new InvalidOperationException("Missing settings view model.");

                settingsViewModel.Formats.Should().OnlyContain(format => !format.IsSelected);
                settingsViewModel.CloseButtonText.Should().Be("Отмена");

                reopened.Close();
                await reopening;
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyCommand_FailedThenSuccessfulApply_KeepsMenuOpenUntilSuccessAndAutomaticallyCloses(bool startupOffer)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaFileAssociationDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            FakeFileAssociationService service = new();
            PicaFileAssociationDialog dialog = new(service, stateService, new RecordingViewModelErrorHandler(), new ImageFormatRegistry());
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = startupOffer
                    ? dialog.ShowIfNeededAsync(owner, CancellationToken.None)
                    : dialog.ShowAsync(owner, CancellationToken.None);
                FileAssociationsWindow window = await WaitForDialogAsync(owner);
                FileAssociationsViewModel viewModel = window.DataContext as FileAssociationsViewModel
                    ?? throw new InvalidOperationException("Missing formats view model.");
                viewModel.CloseButtonText.Should().Be(startupOffer ? "Не сейчас" : "Отмена");
                viewModel.SelectAllCommand.Execute(null);
                service.Failure = new NotSupportedException("Test association rejection");

                await viewModel.ApplyCommand.ExecuteAsync(null);

                window.IsVisible.Should().BeTrue();
                viewModel.HasErrorMessage.Should().BeTrue();

                service.Failure = null;
                await viewModel.ApplyCommand.ExecuteAsync(null);
                await showing.WaitAsync(TimeSpan.FromSeconds(5));

                window.IsVisible.Should().BeFalse();
                owner.OwnedWindows.Should().BeEmpty();
                service.Selected.Should().BeEquivalentTo(service.SupportedExtensions);
                dialog.Completion.IsCompleted.Should().BeTrue();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    private static async Task<FileAssociationsWindow> WaitForDialogAsync(Window owner)
    {
        for (int attempt = 0; attempt < MaximumDialogWaitAttempts; attempt++)
        {
            if (owner.OwnedWindows.OfType<FileAssociationsWindow>().FirstOrDefault() is { } window
                && window.DataContext is FileAssociationsViewModel { IsLoading: false })
            {
                return window;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        throw new TimeoutException("The file association dialog did not finish loading.");
    }
}

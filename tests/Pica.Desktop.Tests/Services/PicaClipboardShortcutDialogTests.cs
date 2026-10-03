using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using Pica.Desktop.Resources;
using Pica.Desktop.Tests.ViewModels;
using Pica.Desktop.Services;
using Pica.Desktop.ViewModels;
using Pica.Desktop.Views;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

[Collection(DesktopHeadlessTestCollection.Name)]
public sealed class PicaClipboardShortcutDialogTests
{
    private const int MaximumDialogWaitAttempts = 500;
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Theory]
    [InlineData("decline")]
    [InlineData("close")]
    [InlineData("escape")]
    [InlineData("enable")]
    public async Task ShowIfNeededAsync_PreviousVersionProfile_RemembersChoiceAcrossRestart(string choice)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaClipboardShortcutDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            string statePath = Path.Combine(directory.DirectoryPath, "desktop.json");
            await File.WriteAllTextAsync(statePath,
                "{\"backgroundIdleTimeoutSeconds\":300,\"hasSeenFileAssociationsPrompt\":true}");
            PicaDesktopStateService stateService = new(statePath, NullLogger<PicaDesktopStateService>.Instance);
            PicaClipboardShortcutDialog dialog = CreateDialog(stateService);
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = dialog.ShowIfNeededAsync(owner, CancellationToken.None);
                ClipboardShortcutOfferWindow window = await WaitForDialogAsync(owner);
                ClipboardShortcutOfferViewModel viewModel = GetViewModel(window);
                window.Title.Should().Be(DesktopUiStrings.ClipboardShortcutTitle);
                window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(text =>
                    text.Text == DesktopUiStrings.ClipboardShortcutDescription);

                switch (choice)
                {
                    case "decline":
                        await viewModel.DeclineCommand.ExecuteAsync(null);
                        break;
                    case "enable":
                        await viewModel.EnableCommand.ExecuteAsync(null);
                        break;
                    case "escape":
                        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                        break;
                    default:
                        window.Close();
                        break;
                }

                await showing.WaitAsync(TimeSpan.FromSeconds(5));
                PicaDesktopState saved = await stateService.LoadAsync(CancellationToken.None);
                saved.HasSeenClipboardShortcutPrompt.Should().BeTrue();
                saved.IsClipboardShortcutEnabled.Should().Be(choice == "enable");
                saved.BackgroundIdleTimeoutSeconds.Should().Be(300);
                await stateService.UpdateAsync(state => state.IsClipboardShortcutEnabled = false, CancellationToken.None);
                PicaDesktopStateService restarted = new(statePath, NullLogger<PicaDesktopStateService>.Instance);

                await CreateDialog(restarted).ShowIfNeededAsync(owner, CancellationToken.None);

                owner.OwnedWindows.Should().BeEmpty();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task ShowIfNeededAsync_AlreadyEnabled_DoesNotPrepareOrShowOffer()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaClipboardShortcutDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            await stateService.UpdateAsync(state => state.IsClipboardShortcutEnabled = true, CancellationToken.None);
            PicaClipboardShortcutDialog dialog = new(stateService,
                _ => throw new InvalidOperationException("An enabled shortcut must not prepare an offer."),
                _ => throw new InvalidOperationException("An enabled shortcut must not enable twice."),
                new RecordingViewModelErrorHandler());
            Window owner = new();

            try
            {
                owner.Show();

                await dialog.ShowIfNeededAsync(owner, CancellationToken.None);

                owner.OwnedWindows.Should().BeEmpty();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task ShowIfNeededAsync_Canceled_DoesNotDismissUnansweredOffer()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaClipboardShortcutDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            using CancellationTokenSource cancellation = new();
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = CreateDialog(stateService).ShowIfNeededAsync(owner, cancellation.Token);
                await WaitForDialogAsync(owner);

                cancellation.Cancel();

                Func<Task> completing = () => showing.WaitAsync(TimeSpan.FromSeconds(5));
                await completing.Should().ThrowAsync<OperationCanceledException>();
                (await stateService.LoadAsync(CancellationToken.None)).HasSeenClipboardShortcutPrompt.Should().BeFalse();
                owner.OwnedWindows.Should().BeEmpty();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task ShowIfNeededAsync_OccupiedShortcut_KeepsOfferOpenWithRedError()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaClipboardShortcutDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            PicaClipboardShortcutDialog dialog = new(stateService,
                _ => Task.FromResult<IReadOnlyList<ViewerSettingContribution>>(Array.Empty<ViewerSettingContribution>()),
                _ => Task.FromException(new PicaShortcutException(PicaShortcutFailure.Occupied)),
                new RecordingViewModelErrorHandler());
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = dialog.ShowIfNeededAsync(owner, CancellationToken.None);
                ClipboardShortcutOfferWindow window = await WaitForDialogAsync(owner);
                ClipboardShortcutOfferViewModel viewModel = GetViewModel(window);

                await viewModel.EnableCommand.ExecuteAsync(null);
                window.UpdateLayout();

                window.IsVisible.Should().BeTrue();
                (await stateService.LoadAsync(CancellationToken.None)).HasSeenClipboardShortcutPrompt.Should().BeFalse();
                TextBlock error = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("viewer-error"));
                error.IsVisible.Should().BeTrue();
                error.Foreground.Should().BeOfType<SolidColorBrush>().Which.Color.Should().Be(Color.Parse("#FFE5484D"));

                window.Close();
                await showing;
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public async Task ShowIfNeededAsync_ClosedDuringRecording_ReleasesRecorderBeforeCompleting()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(PicaClipboardShortcutDialogTests), SessionLock, async () =>
        {
            using PicaTemporaryDirectory directory = new();
            PicaDesktopStateService stateService = new(Path.Combine(directory.DirectoryPath, "desktop.json"),
                NullLogger<PicaDesktopStateService>.Instance);
            TaskCompletionSource recording = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool released = false;
            ViewerRecordedSettingContribution<string> setting = new("Сочетание для вставки", "Ctrl+Shift+V",
                async (_, ct) =>
                {
                    recording.SetResult();

                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                        return "new";
                    }
                    finally
                    {
                        released = true;
                    }
                }, (_, _) => Task.CompletedTask, value => value, _ => "Ошибка", NullLogger.Instance);
            PicaClipboardShortcutDialog dialog = new(stateService,
                _ => Task.FromResult<IReadOnlyList<ViewerSettingContribution>>(new ViewerSettingContribution[] { setting }),
                _ => Task.CompletedTask, new RecordingViewModelErrorHandler());
            Window owner = new();

            try
            {
                owner.Show();
                Task showing = dialog.ShowIfNeededAsync(owner, CancellationToken.None);
                ClipboardShortcutOfferWindow window = await WaitForDialogAsync(owner);
                Button record = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Изменить"));
                record.Focus();
                record.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await recording.Task.WaitAsync(TimeSpan.FromSeconds(5));
                GetViewModel(window).IsRecording.Should().BeTrue();
                GetViewModel(window).EnableCommand.CanExecute(null).Should().BeFalse();

                window.Close();
                await showing.WaitAsync(TimeSpan.FromSeconds(5));

                released.Should().BeTrue();
                dialog.Completion.IsCompleted.Should().BeTrue();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    private static PicaClipboardShortcutDialog CreateDialog(IPicaDesktopStateService stateService)
    {
        return new PicaClipboardShortcutDialog(stateService,
            _ => Task.FromResult<IReadOnlyList<ViewerSettingContribution>>(Array.Empty<ViewerSettingContribution>()),
            ct => stateService.UpdateAsync(state => state.IsClipboardShortcutEnabled = true, ct),
            new RecordingViewModelErrorHandler());
    }

    private static ClipboardShortcutOfferViewModel GetViewModel(ClipboardShortcutOfferWindow window)
    {
        return window.DataContext as ClipboardShortcutOfferViewModel
            ?? throw new InvalidOperationException("Missing clipboard offer view model.");
    }

    private static async Task<ClipboardShortcutOfferWindow> WaitForDialogAsync(Window owner)
    {
        for (int attempt = 0; attempt < MaximumDialogWaitAttempts; attempt++)
        {
            if (owner.OwnedWindows.OfType<ClipboardShortcutOfferWindow>().FirstOrDefault() is { } window)
            {
                return window;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        throw new TimeoutException("The clipboard offer did not open.");
    }
}

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Controls;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerRecordedSettingControlTests
{
    private const int LeadingSettingCount = 12;
    private const double RestrictedWindowWidth = 360d;
    private const double RestrictedWindowHeight = 400d;

    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordAsync_WithSuccessfulOrFailedApply_UpdatesOnlyAfterSuccess(bool reject)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerRecordedSettingControlTests), SessionLock, async () =>
        {
            ViewerRecordedSettingControl<string> control = CreateControl(
                (_, _) => Task.FromResult("new"),
                (_, _) => reject ? Task.FromException(new IOException("Test save failed")) : Task.CompletedTask);

            await control.RecordAsync(nint.Zero, CancellationToken.None);

            control.ValueText.Text.Should().Be(reject ? "old" : "new");
            control.ErrorText.IsVisible.Should().Be(reject);
            control.ErrorText.Text.Should().Be(reject ? "Could not save" : null);
            control.RecordButton.IsEnabled.Should().BeTrue();
            control.RecordButton.Content.Should().Be("Изменить");
        });
    }

    [Theory]
    [InlineData("button")]
    [InlineData("focus")]
    [InlineData("hide")]
    [InlineData("close")]
    public async Task RecordAsync_WhenCanceled_ReleasesRecordingWithoutApplying(string reason)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerRecordedSettingControlTests), SessionLock, async () =>
        {
            bool applied = false;
            ViewerRecordedSettingControl<string> control = CreateControl(
                async (_, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return "new";
                },
                (_, _) =>
                {
                    applied = true;
                    return Task.CompletedTask;
                });
            StackPanel panel = new();
            panel.Children.Add(control.Control);
            TextBox other = new();
            panel.Children.Add(other);
            Window window = new() { Content = panel };
            window.Show();
            control.RecordButton.Focus();
            Task recording = control.RecordAsync(nint.Zero, CancellationToken.None);
            ViewerSettingRecording.IsActive(window).Should().BeTrue();
            control.Completion.IsCompleted.Should().BeFalse();
            control.RecordButton.Content.Should().Be("Нажми сочетание клавиш");

            switch (reason)
            {
                case "button":
                    await control.RecordAsync(nint.Zero, CancellationToken.None);
                    break;
                case "focus":
                    other.Focus();
                    break;
                case "hide":
                    panel.IsVisible = false;
                    break;
                case "close":
                    window.Close();
                    break;
            }

            await recording;
            await control.Completion;

            applied.Should().BeFalse();
            control.ValueText.Text.Should().Be("old");
            control.ErrorText.IsVisible.Should().BeFalse();
            ViewerSettingRecording.IsActive(window).Should().BeFalse();
            window.Close();
        });
    }

    [Fact]
    public async Task RecordAsync_WhenRecorderCancelsWithoutCancelingCaller_DoesNotDisplayAnError()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerRecordedSettingControlTests), SessionLock, async () =>
        {
            ViewerRecordedSettingControl<string> control = CreateControl(
                (_, _) => Task.FromCanceled<string>(new CancellationToken(true)), (_, _) => Task.CompletedTask);

            await control.RecordAsync(nint.Zero, CancellationToken.None);

            control.ErrorText.IsVisible.Should().BeFalse();
            control.ValueText.Text.Should().Be("old");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordAsync_WhenClosedOrEscapedDuringApply_CancelsApply(bool closeWindow)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerRecordedSettingControlTests), SessionLock, async () =>
        {
            TaskCompletionSource applying = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool canceled = false;
            ViewerRecordedSettingControl<string> control = CreateControl(
                (_, _) => Task.FromResult("new"),
                async (_, ct) =>
                {
                    applying.SetResult();

                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        canceled = true;
                        throw;
                    }
                });
            Window window = new() { Content = control.Control };
            window.Show();
            Task recording = control.RecordAsync(nint.Zero, CancellationToken.None);
            await applying.Task;

            if (closeWindow)
            {
                window.Close();
            }
            else
            {
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            }

            await recording;

            canceled.Should().BeTrue();
            control.ValueText.Text.Should().Be("old");
            control.ErrorText.IsVisible.Should().BeFalse();
            ViewerSettingRecording.IsActive(window).Should().BeFalse();
            window.Close();
        });
    }

    [Fact]
    public async Task RecordAsync_WithFailureInShortWindow_ScrollsErrorIntoView()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(SkiaViewerTestSession), SessionLock, async () =>
        {
            ViewerRecordedSettingControl<string> control = CreateControl(
                (_, _) => Task.FromResult("new"),
                (_, _) => Task.FromException(new IOException("Test save failed")));
            List<ViewerSettingControl> settings = [];

            for (int index = 0; index < LeadingSettingCount; index++)
            {
                settings.Add(CreateControl((_, _) => Task.FromResult("old"), (_, _) => Task.CompletedTask));
            }

            settings.Add(control);
            ViewerSettingsPanel panel = new(settings);
            Window window = new() { Width = RestrictedWindowWidth, Height = RestrictedWindowHeight, Content = panel };
            window.Show();
            window.UpdateLayout();

            await control.RecordAsync(nint.Zero, CancellationToken.None);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            ScrollViewer scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.Extent.Height.Should().BeGreaterThan(scroll.Viewport.Height);
            scroll.Offset.Y.Should().BeGreaterThan(0d);
            Point? errorPosition = control.ErrorText.TranslatePoint(default, window);
            errorPosition.Should().NotBeNull();
            errorPosition.GetValueOrDefault().Y.Should().BeGreaterThanOrEqualTo(0d);
            (errorPosition.GetValueOrDefault().Y + control.ErrorText.Bounds.Height).Should().BeLessThanOrEqualTo(window.ClientSize.Height);
            window.Close();
        });
    }

    private static ViewerRecordedSettingControl<string> CreateControl(
        Func<nint, CancellationToken, Task<string>> record,
        Func<string, CancellationToken, Task> apply)
    {
        ViewerRecordedSettingContribution<string> contribution = new(
            "Recorded value", "old", record, apply, value => value,
            _ => "Could not save", NullLogger.Instance);
        return (ViewerRecordedSettingControl<string>)contribution.CreateControl();
    }
}

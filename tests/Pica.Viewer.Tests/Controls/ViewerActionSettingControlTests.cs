using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Controls;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerActionSettingControlTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<ViewerTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task ExecuteAsync_Error_DisablesButtonWhileBusyAndShowsSafeMessage()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerActionSettingControlTests), SessionLock, async () =>
        {
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Window? actualOwner = null;
            ViewerActionSettingContribution contribution = new("Formats", (owner, _) =>
            {
                actualOwner = owner;
                return completion.Task;
            }, _ => "Безопасная ошибка", NullLogger.Instance);
            StackPanel panel = contribution.CreateControl().Control as StackPanel
                ?? throw new InvalidOperationException("Missing action panel.");
            Button button = panel.Children.OfType<Button>().Single();
            IAsyncRelayCommand command = button.Command as IAsyncRelayCommand
                ?? throw new InvalidOperationException("Missing async action command.");
            Window window = new() { Content = panel };

            try
            {
                window.Show();

                Task operation = command.ExecuteAsync(null);

                command.CanExecute(null).Should().BeFalse();
                button.IsEffectivelyEnabled.Should().BeFalse();
                actualOwner.Should().BeSameAs(window);

                completion.SetException(new IOException("Private path"));
                await operation;

                panel.Children.OfType<ViewerSettingErrorControl>().Single().Text.Should().Be("Безопасная ошибка");
                command.CanExecute(null).Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Create_FooterContribution_PlacesActionAfterExistingSettings()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerActionSettingControlTests), SessionLock, () =>
        {
            ImageViewerState state = new();
            ImageViewerSession session = new(new PicaViewerRequest(Array.Empty<PicaImageItem>(), Guid.Empty), true);
            using ImageViewerSettingsViewModel settings = new(new RecordingImageViewerStateService(state), session,
                new RecordingImageLoadingSettings(), new ViewerWindowPlacementProvider(new ViewerWindowPlacement(true, null, null, null, null)),
                new RecordingViewModelErrorHandler(), state);
            ViewerActionSettingContribution footer = new("Formats", (_, _) => Task.CompletedTask, _ => "Error", NullLogger.Instance);

            IReadOnlyList<ViewerSettingControl> controls = ViewerSettingsControlFactory.Create(settings,
                new ViewerSettingContribution[] { footer });

            controls.Last().Should().BeOfType<ViewerActionSettingControl>();
            controls.Count(control => control is ViewerActionSettingControl).Should().Be(1);
            controls.First().Should().BeOfType<ViewerChoiceSettingControl<int>>();
        });
    }
}

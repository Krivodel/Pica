using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using FluentAssertions;
using Xunit;

using Pica.Tests.Common;
using Pica.Viewer.Controls;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Controls;

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class ViewerCheckBoxSettingControlTests
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "failure")]
    [InlineData(true, "canceled")]
    public async Task ChangeValue_WhileApplying_DisablesUntilOperationFinishes(bool initialValue, string outcome)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerCheckBoxSettingControlTests), SessionLock, async () =>
        {
            TaskCompletionSource applying = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource reenabled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ViewerCheckBoxSettingContribution contribution = new("Global paste", initialValue,
                (_, _) => applying.Task, NullLogger.Instance, _ => "Не удалось сохранить настройку.",
                dependentSettings: new ViewerSettingContribution[]
                {
                    new ViewerCheckBoxSettingContribution("Fullscreen", false, (_, _) => Task.CompletedTask, NullLogger.Instance)
                });
            ViewerCheckBoxSettingControl control = (ViewerCheckBoxSettingControl)contribution.CreateControl();
            control.CheckBox.PropertyChanged += (_, e) =>
            {
                if ((e.Property == InputElement.IsEnabledProperty) && (e.NewValue is true))
                {
                    reenabled.TrySetResult();
                }
            };

            control.CheckBox.IsChecked = !initialValue;

            control.CheckBox.IsEnabled.Should().BeFalse();
            control.DependentSettingsPanel?.IsVisible.Should().Be(initialValue);

            switch (outcome)
            {
                case "failure":
                    applying.SetException(new IOException("Test save failure"));
                    break;
                case "canceled":
                    applying.SetCanceled();
                    break;
                default:
                    applying.SetResult();
                    break;
            }

            await reenabled.Task.WaitAsync(TimeSpan.FromSeconds(5));

            bool expectedValue = outcome is "success" ? !initialValue : initialValue;
            control.CheckBox.IsEnabled.Should().BeTrue();
            control.CheckBox.IsChecked.Should().Be(expectedValue);
            control.DependentSettingsPanel?.IsVisible.Should().Be(expectedValue);
            control.ErrorText?.IsVisible.Should().Be(outcome is "failure");
        });
    }

    [Fact]
    public async Task ChangeValue_WithDependentSettings_HidesAndRestoresWholeSettingsGroup()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerCheckBoxSettingControlTests), SessionLock, () =>
        {
            ViewerCheckBoxSettingContribution dependent = new("Fullscreen", true,
                (_, _) => Task.CompletedTask, NullLogger.Instance);
            ViewerCheckBoxSettingContribution contribution = new("Global paste", false,
                (_, _) => Task.CompletedTask, NullLogger.Instance,
                dependentSettings: new ViewerSettingContribution[] { dependent });
            ViewerCheckBoxSettingControl control = (ViewerCheckBoxSettingControl)contribution.CreateControl();
            StackPanel panel = control.DependentSettingsPanel
                ?? throw new InvalidOperationException("The dependent settings panel was not created.");
            panel.IsVisible.Should().BeFalse();

            control.CheckBox.IsChecked = true;

            panel.IsVisible.Should().BeTrue();
            control.CheckBox.IsChecked = false;
            panel.IsVisible.Should().BeFalse();
            control.CheckBox.IsChecked = true;
            panel.IsVisible.Should().BeTrue();
            CheckBox actualChild = panel.Children[0].Should().BeOfType<CheckBox>().Subject;
            actualChild.IsChecked.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ChangeValue_WhenDisablingFails_KeepsDependentSettingsVisible()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerCheckBoxSettingControlTests), SessionLock, () =>
        {
            ViewerCheckBoxSettingContribution contribution = new("Global paste", true,
                (_, _) => Task.FromException(new IOException("Test save failure")), NullLogger.Instance,
                _ => "Не удалось сохранить настройку.", dependentSettings: new ViewerSettingContribution[]
                {
                    new ViewerCheckBoxSettingContribution("Fullscreen", true, (_, _) => Task.CompletedTask, NullLogger.Instance)
                });
            ViewerCheckBoxSettingControl control = (ViewerCheckBoxSettingControl)contribution.CreateControl();

            control.CheckBox.IsChecked = false;

            control.CheckBox.IsChecked.Should().BeTrue();
            control.DependentSettingsPanel?.IsVisible.Should().BeTrue();
            control.ErrorText?.IsVisible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ChangeValue_WhenValidationFails_DisplaysSafeErrorAndKeepsValue()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerCheckBoxSettingControlTests), SessionLock, () =>
        {
            ViewerCheckBoxSettingContribution contribution = new(
                "Clipboard shortcut", false,
                (_, _) => Task.FromException(new IOException("Private failure details")),
                NullLogger.Instance, _ => "Shortcut is unavailable");
            ViewerCheckBoxSettingControl control = (ViewerCheckBoxSettingControl)contribution.CreateControl();

            control.CheckBox.IsChecked = true;

            control.CheckBox.IsChecked.Should().BeFalse();
            TextBlock error = control.ErrorText ?? throw new InvalidOperationException("The error control was not created.");
            error.IsVisible.Should().BeTrue();
            error.Text.Should().Be("Shortcut is unavailable");
        });
    }

    [Fact]
    public async Task ChangeValue_WhenShortcutChangeFails_RollsBackVisibleCheckBox()
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(typeof(ViewerCheckBoxSettingControlTests), SessionLock, () =>
        {
            ViewerCheckBoxSettingContribution contribution = new(
                "Clipboard shortcut", false,
                (_, _) => Task.FromException(new IOException("The test shortcut cannot be created.")),
                NullLogger.Instance);
            ViewerCheckBoxSettingControl control = (ViewerCheckBoxSettingControl)contribution.CreateControl();

            control.CheckBox.IsChecked = true;

            control.CheckBox.IsChecked.Should().BeFalse();
        });
    }
}

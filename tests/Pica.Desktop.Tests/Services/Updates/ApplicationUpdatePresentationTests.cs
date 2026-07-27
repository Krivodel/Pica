using FluentAssertions;
using Xunit;

using Pica.Desktop.Resources;
using Pica.Desktop.Services.Updates;

namespace Pica.Desktop.Tests.Services.Updates;

public sealed class ApplicationUpdatePresentationTests
{
    [Fact]
    public void Constructor_WithVersion_PresentsAvailableVersion()
    {
        ApplicationUpdatePresentation presentation = new("1.2.3");

        presentation.Message.Should().Contain("1.2.3");
        presentation.IsProgressVisible.Should().BeFalse();
        presentation.AreActionsEnabled.Should().BeTrue();
        presentation.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void ShowDownloadProgress_PresentsDeterminateProgress()
    {
        ApplicationUpdatePresentation presentation = new("1.2.3");

        presentation.ShowDownloadProgress(42);

        presentation.Message.Should().Be(DesktopUiStrings.UpdateDownloading);
        presentation.IsProgressVisible.Should().BeTrue();
        presentation.IsProgressIndeterminate.Should().BeFalse();
        presentation.DownloadProgress.Should().Be(42);
        presentation.AreActionsEnabled.Should().BeFalse();
        presentation.IsBusy.Should().BeTrue();
    }

    [Fact]
    public void ShowInstalling_PresentsIndeterminateProgress()
    {
        ApplicationUpdatePresentation presentation = new("1.2.3");

        presentation.ShowInstalling();

        presentation.Message.Should().Be(DesktopUiStrings.UpdateInstalling);
        presentation.IsProgressVisible.Should().BeTrue();
        presentation.IsProgressIndeterminate.Should().BeTrue();
        presentation.AreActionsEnabled.Should().BeFalse();
        presentation.IsBusy.Should().BeTrue();
    }

    [Fact]
    public void ShowInstallFailure_AllowsRetryOrDismissal()
    {
        ApplicationUpdatePresentation presentation = new("1.2.3");
        presentation.ShowDownloadProgress(42);

        presentation.ShowInstallFailure();

        presentation.Message.Should().Be(DesktopUiStrings.UpdateInstallFailed);
        presentation.IsProgressVisible.Should().BeFalse();
        presentation.IsProgressIndeterminate.Should().BeFalse();
        presentation.AreActionsEnabled.Should().BeTrue();
        presentation.IsBusy.Should().BeFalse();
    }
}

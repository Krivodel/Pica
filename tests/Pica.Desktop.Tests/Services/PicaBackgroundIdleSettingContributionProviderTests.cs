using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaBackgroundIdleSettingContributionProviderTests
{
    [Fact]
    public async Task CreateAsync_WithDefaultState_ProvidesDesktopSetting()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        PicaDesktopStateService stateService = CreateStateService(
            temporaryDirectory);
        PicaBackgroundIdleSettingContributionProvider provider = new(
            stateService,
            NullLogger<
                PicaBackgroundIdleSettingContributionProvider>.Instance);

        IReadOnlyList<ViewerSettingContribution> contributions =
            await provider.CreateAsync(CancellationToken.None);

        ViewerChoiceSettingContribution<int> contribution =
            contributions.Should().ContainSingle().Which
                .Should()
                .BeOfType<ViewerChoiceSettingContribution<int>>()
                .Subject;
        contribution.Label.Should().Be(
            "Оставаться в фоне после закрытия");
        contribution.InitialValue.Should().Be(60);
        contribution.Choices.Select(choice => choice.Value)
            .Should()
            .Equal(0, 15, 60, 300);
        contribution.Choices.Single(choice => choice.Value == 0)
            .DisplayName.Should().Be("Не оставаться в фоне");
    }

    [Fact]
    public async Task ApplyAsync_WithValue_SavesDesktopState()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        PicaDesktopStateService stateService = CreateStateService(
            temporaryDirectory);
        PicaBackgroundIdleSettingContributionProvider provider = new(
            stateService,
            NullLogger<
                PicaBackgroundIdleSettingContributionProvider>.Instance);
        IReadOnlyList<ViewerSettingContribution> contributions =
            await provider.CreateAsync(CancellationToken.None);
        ViewerChoiceSettingContribution<int> contribution =
            (ViewerChoiceSettingContribution<int>)contributions.Single();

        await contribution.ApplyAsync(300, CancellationToken.None);
        PicaDesktopStateService reader = CreateStateService(
            temporaryDirectory);
        PicaDesktopState state = await reader.LoadAsync(
            CancellationToken.None);

        state.BackgroundIdleTimeoutSeconds.Should().Be(300);
    }

    private static PicaDesktopStateService CreateStateService(
        PicaTemporaryDirectory temporaryDirectory)
    {
        string stateFilePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "desktop.json");

        return new PicaDesktopStateService(
            stateFilePath,
            NullLogger<PicaDesktopStateService>.Instance);
    }
}

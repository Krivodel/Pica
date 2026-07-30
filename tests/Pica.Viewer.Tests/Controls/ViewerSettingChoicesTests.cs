using FluentAssertions;
using Xunit;

using Pica.Viewer.Controls;

namespace Pica.Viewer.Tests.Controls;

public sealed class ViewerSettingChoicesTests
{
    [Fact]
    public void BackgroundIdleTimeoutOptions_WhenListed_ContainsSupportedChoices()
    {
        int[] values = ViewerSettingChoices
            .BackgroundIdleTimeoutOptions
            .Select(option => option.Value)
            .ToArray();

        values.Should().Equal(0, 15, 60, 300);
    }

    [Fact]
    public void BackgroundIdleTimeoutOptions_WithDefaultValue_DisplaysOneMinute()
    {
        ViewerSettingOption<int> defaultOption =
            ViewerSettingChoices.BackgroundIdleTimeoutOptions
                .Single(option => option.Value == 60);

        defaultOption.DisplayName.Should().Be("1 минута");
    }

    [Fact]
    public void BackgroundIdleTimeoutOptions_WithZero_DisplaysDisabledChoice()
    {
        ViewerSettingOption<int> disabledOption =
            ViewerSettingChoices.BackgroundIdleTimeoutOptions
                .Single(option => option.Value == 0);

        disabledOption.DisplayName.Should().Be("Не оставаться в фоне");
    }
}

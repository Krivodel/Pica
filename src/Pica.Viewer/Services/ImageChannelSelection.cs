namespace Pica.Viewer.Services;

internal sealed class ImageChannelSelection
{
    internal bool IsActive { get; private set; }
    internal bool IsAvailabilityKnown { get; private set; }
    internal ImageChannel? SelectedChannel =>
        IsActive ? _availableChannels[_selectedIndex] : null;

    private IReadOnlyList<ImageChannel> _availableChannels = ImageChannel.ColorChannels;
    private int _selectedIndex;

    internal void Enter()
    {
        _availableChannels = ImageChannel.ColorChannels;
        _selectedIndex = 0;
        IsAvailabilityKnown = false;
        IsActive = true;
    }

    internal void Exit()
    {
        IsActive = false;
    }

    internal void Navigate(int direction)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException(
                "An image channel can be selected only while channel mode is active.");
        }

        if (direction == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "The channel navigation direction must not be zero.");
        }

        int step = direction < 0 ? -1 : 1;
        _selectedIndex = (
            _selectedIndex
            + step
            + _availableChannels.Count)
            % _availableChannels.Count;
    }

    internal void SetHasAlpha(bool hasAlpha)
    {
        _availableChannels = hasAlpha
            ? ImageChannel.ColorAndAlphaChannels
            : ImageChannel.ColorChannels;
        _selectedIndex = Math.Clamp(
            _selectedIndex,
            0,
            _availableChannels.Count - 1);
        IsAvailabilityKnown = true;
    }
}

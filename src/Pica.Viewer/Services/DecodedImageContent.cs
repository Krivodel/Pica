namespace Pica.Viewer.Services;

internal sealed class DecodedImageContent : IDisposable
{
    internal IReadOnlyList<DecodedImageContentGroup> Groups { get; }
    internal int InitialGroupIndex { get; }
    internal bool IsMixed =>
        Groups.Any(group =>
            group.Definition.Kind
            == ImageContentGroupKind.StillImages)
        && Groups.Any(group =>
            group.Definition.Kind
            == ImageContentGroupKind.Animation);

    internal DecodedImageContent(
        IReadOnlyList<DecodedImageContentGroup> groups,
        int initialGroupIndex)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (groups.Count == 0)
        {
            throw new ArgumentException(
                "Decoded image content must contain at least one group.",
                nameof(groups));
        }

        if ((initialGroupIndex < 0)
            || (initialGroupIndex >= groups.Count))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialGroupIndex),
                initialGroupIndex,
                $"The initial group index must be between 0 and {groups.Count - 1}.");
        }

        Groups = groups;
        InitialGroupIndex = initialGroupIndex;
    }

    internal static DecodedImageContent CreateSingle(
        DecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ImageContentGroupKind kind = image.FramePresentationMode.HasFlag(
            ImageFramePresentationModes.AutomaticPlayback)
                ? ImageContentGroupKind.Animation
                : ImageContentGroupKind.StillImages;
        ImageContentGroupDefinition definition = new(
            kind,
            image.FrameCount,
            image.FrameNumbering);

        return new DecodedImageContent(
            [new DecodedImageContentGroup(definition, image)],
            0);
    }

    public void Dispose()
    {
        foreach (DecodedImageContentGroup group in Groups)
        {
            foreach (DecodedImage image
                in group.StopAndGetLoadedImages())
            {
                image.Dispose();
            }
        }
    }
}

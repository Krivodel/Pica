using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingMultiFrameImageDecoder :
    IMultiFrameImageDecoder
{
    internal IReadOnlyList<int> SelectedTrackIndices =>
        _selectedTrackIndices.AsReadOnly();

    private readonly Func<DecodedImage> _createImage;
    private readonly List<int> _selectedTrackIndices = [];

    internal RecordingMultiFrameImageDecoder(
        Func<DecodedImage> createImage)
    {
        _createImage = createImage
            ?? throw new ArgumentNullException(nameof(createImage));
    }

    public DecodedImage Decode(
        Stream sourceStream,
        ImageDecoderSelection decoderSelection,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ArgumentNullException.ThrowIfNull(decoderSelection);
        ct.ThrowIfCancellationRequested();
        IsoBmffAnimationTrackSelection trackSelection =
            decoderSelection.AnimationTrackSelection
            ?? throw new InvalidOperationException(
                "The test decoder expected an animation track selection.");
        _selectedTrackIndices.Add(trackSelection.TrackIndex);

        return _createImage();
    }
}

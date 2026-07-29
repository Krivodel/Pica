using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class StubImagePresentationInfo :
    IImagePresentationInfo
{
    public PicaImageItem? CurrentItem { get; private set; }
    public ImageDimensions SourceDimensions { get; private set; }

    public event EventHandler? Changed;

    internal void SetPresentation(
        PicaImageItem? currentItem,
        ImageDimensions sourceDimensions)
    {
        CurrentItem = currentItem;
        SourceDimensions = sourceDimensions;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

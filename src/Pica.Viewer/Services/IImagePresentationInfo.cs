using Pica.Protocol;

namespace Pica.Viewer.Services;

internal interface IImagePresentationInfo
{
    PicaImageItem? CurrentItem { get; }
    ImageDimensions SourceDimensions { get; }

    event EventHandler? Changed;
}

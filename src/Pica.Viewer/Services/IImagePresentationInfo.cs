using Pica.Protocol;

namespace Pica.Viewer.Services;

internal interface IImagePresentationInfo
{
    PicaImageItem? CurrentItem { get; }
    ImageDimensions SourceDimensions { get; }
    bool IsCurrentImageFileBacked { get; }

    event EventHandler? Changed;
}

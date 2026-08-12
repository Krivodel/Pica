namespace Pica.Viewer.Services;

internal interface IImageDecoderResolver
{
    ImageDecoderSelection Resolve(string fileName);
}

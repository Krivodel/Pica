namespace Pica.Viewer.Services;

internal interface IImageDecoderResolver
{
    IImageDecoder Resolve(string fileName);
}

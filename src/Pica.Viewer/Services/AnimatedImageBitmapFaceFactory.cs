using AnimatedImage;

namespace Pica.Viewer.Services;

internal sealed class AnimatedImageBitmapFaceFactory :
    IBitmapFaceFactory
{
    public IBitmapFace Create(int width, int height)
    {
        return new AnimatedImageBitmapFace(
            width,
            height);
    }
}

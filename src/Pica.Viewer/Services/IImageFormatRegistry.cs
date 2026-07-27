namespace Pica.Viewer.Services;

public interface IImageFormatRegistry
{
    bool IsSupportedFileName(string fileName);

    string GetContentType(string fileName);

    string GetExtension(string fileName);
}

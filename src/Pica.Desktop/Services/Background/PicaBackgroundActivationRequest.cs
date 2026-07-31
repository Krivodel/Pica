namespace Pica.Desktop.Services.Background;

internal sealed record PicaBackgroundActivationRequest(
    IReadOnlyList<string> Arguments,
    long? SourceWindowHandle = null);

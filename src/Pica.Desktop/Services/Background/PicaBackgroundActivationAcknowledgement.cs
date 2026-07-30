namespace Pica.Desktop.Services.Background;

internal sealed record PicaBackgroundActivationAcknowledgement
{
    public static PicaBackgroundActivationAcknowledgement Instance { get; } = new();
}

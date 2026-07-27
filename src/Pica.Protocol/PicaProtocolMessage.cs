namespace Pica.Protocol;

public sealed record PicaProtocolMessage<T>(
    int Version,
    T Payload);

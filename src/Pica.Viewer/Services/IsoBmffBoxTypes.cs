namespace Pica.Viewer.Services;

internal static class IsoBmffBoxTypes
{
    internal const uint AuxiliaryReference = 0x6175786C;
    internal const uint Edit = 0x65647473;
    internal const uint EditList = 0x656C7374;
    internal const uint Handler = 0x68646C72;
    internal const uint Media = 0x6D646961;
    internal const uint MediaHeader = 0x6D646864;
    internal const uint MediaInformation = 0x6D696E66;
    internal const uint Meta = 0x6D657461;
    internal const uint Movie = 0x6D6F6F76;
    internal const uint Free = 0x66726565;
    internal const uint SampleDescription = 0x73747364;
    internal const uint SampleSize = 0x7374737A;
    internal const uint SampleTable = 0x7374626C;
    internal const uint TimeToSample = 0x73747473;
    internal const uint Track = 0x7472616B;
    internal const uint TrackHeader = 0x746B6864;
    internal const uint TrackReference = 0x74726566;
}

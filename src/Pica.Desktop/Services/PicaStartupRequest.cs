using Pica.Protocol;

namespace Pica.Desktop.Services;

public sealed record PicaStartupRequest(
    PicaViewerRequest ViewerRequest,
    PicaHostConnection? HostConnection);

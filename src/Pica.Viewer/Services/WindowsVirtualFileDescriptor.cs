namespace Pica.Viewer.Services;

internal sealed record WindowsVirtualFileDescriptor(
    string FileName,
    ulong? DeclaredSize,
    bool IsDirectory);

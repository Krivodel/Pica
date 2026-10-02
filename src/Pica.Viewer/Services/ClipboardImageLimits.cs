namespace Pica.Viewer.Services;

internal static class ClipboardImageLimits
{
    internal const int MaximumInputBytes = 128 * 1024 * 1024;
    internal const int MaximumCandidates = 64;
    internal const int MaximumRedirects = 5;
    internal const int MaximumDescriptorBytes = 64 * 1024;

    internal static TimeSpan DownloadTimeout => TimeSpan.FromSeconds(30);
}

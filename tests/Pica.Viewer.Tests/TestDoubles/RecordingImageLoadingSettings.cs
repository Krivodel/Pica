using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class RecordingImageLoadingSettings : IImageLoadingSettings
{
    internal bool? IsFastLoadingEnabled { get; private set; }

    public void SetFastLoadingEnabled(bool isFastLoadingEnabled)
    {
        IsFastLoadingEnabled = isFastLoadingEnabled;
    }
}

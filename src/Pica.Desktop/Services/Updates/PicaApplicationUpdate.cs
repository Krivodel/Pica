using Velopack;

namespace Pica.Desktop.Services.Updates;

internal sealed class PicaApplicationUpdate
{
    public string Version { get; }

    internal UpdateInfo NativeUpdate { get; }

    internal PicaApplicationUpdate(UpdateInfo nativeUpdate)
    {
        ArgumentNullException.ThrowIfNull(nativeUpdate);

        NativeUpdate = nativeUpdate;
        Version = nativeUpdate.TargetFullRelease.Version.ToString();
    }
}

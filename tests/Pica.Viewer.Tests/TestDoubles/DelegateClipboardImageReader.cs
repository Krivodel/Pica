using Avalonia.Input.Platform;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.TestDoubles;

internal sealed class DelegateClipboardImageReader : IClipboardImageReader
{
    internal Func<CancellationToken, Task<IReadOnlyList<ClipboardImageInput>>> Read { get; set; } =
        _ => Task.FromResult<IReadOnlyList<ClipboardImageInput>>(Array.Empty<ClipboardImageInput>());

    public async Task<IReadOnlyList<ClipboardImageInput>> ReadAsync(IClipboard clipboard, CancellationToken ct)
    {
        return await Read(ct).ConfigureAwait(false);
    }
}

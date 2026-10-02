namespace Pica.Viewer.Services;

internal sealed class ClipboardDataSnapshot : IDisposable
{
    internal uint? SequenceNumber { get; set; }
    internal List<ClipboardImageInput> Files { get; } = [];
    internal List<ClipboardImageInput> Images { get; } = [];
    internal List<ClipboardImageInput> Rasters { get; } = [];
    internal List<string> Text { get; } = [];

    public void Dispose()
    {
        foreach (ClipboardImageInput input in Files.Concat(Images).Concat(Rasters))
        {
            input.Dispose();
        }

        Files.Clear();
        Images.Clear();
        Rasters.Clear();
        Text.Clear();
    }

    internal void TransferTo(ClipboardDataSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (ClipboardImageInput input in Files)
        {
            if ((input.FilePath is not null) && target.Files.Any(candidate =>
                    string.Equals(candidate.FilePath, input.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                input.Dispose();
            }
            else
            {
                target.Files.Add(input);
            }
        }

        target.Images.AddRange(Images);
        target.Rasters.AddRange(Rasters);
        target.Text.AddRange(Text);
        Files.Clear();
        Images.Clear();
        Rasters.Clear();
        Text.Clear();
    }
}

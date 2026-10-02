namespace Pica.Desktop.Services.FileAssociations;

internal sealed record WindowsFileAssociationSnapshot(
    string? UserChoice,
    string? DefaultProgram,
    string? UserClassProgram,
    IReadOnlyList<WindowsFileAssociationRegistryValue> Candidates)
    : FileAssociationSnapshot(UserChoice, DefaultProgram, UserClassProgram)
{
    public override bool Matches(FileAssociationSnapshot other)
    {
        return base.Matches(other)
            && other is WindowsFileAssociationSnapshot snapshot
            && Candidates.Count == snapshot.Candidates.Count
            && Candidates.Zip(snapshot.Candidates).All(pair => pair.First.Matches(pair.Second));
    }
}

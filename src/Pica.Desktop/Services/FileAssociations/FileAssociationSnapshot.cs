namespace Pica.Desktop.Services.FileAssociations;

internal record FileAssociationSnapshot(
    string? UserChoice,
    string? DefaultProgram,
    string? UserClassProgram)
{
    public virtual bool Matches(FileAssociationSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(UserChoice, other.UserChoice, StringComparison.OrdinalIgnoreCase)
            && string.Equals(DefaultProgram, other.DefaultProgram, StringComparison.OrdinalIgnoreCase)
            && string.Equals(UserClassProgram, other.UserClassProgram, StringComparison.OrdinalIgnoreCase);
    }
}

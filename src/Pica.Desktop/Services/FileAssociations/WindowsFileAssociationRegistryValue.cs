using System.Collections;

using Microsoft.Win32;

namespace Pica.Desktop.Services.FileAssociations;

internal sealed record WindowsFileAssociationRegistryValue(
    string KeyPath,
    string ProgramId,
    object Data,
    RegistryValueKind Kind)
{
    public bool Matches(WindowsFileAssociationRegistryValue other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(KeyPath, other.KeyPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ProgramId, other.ProgramId, StringComparison.OrdinalIgnoreCase)
            && Kind == other.Kind
            && StructuralComparisons.StructuralEqualityComparer.Equals(Data, other.Data);
    }
}

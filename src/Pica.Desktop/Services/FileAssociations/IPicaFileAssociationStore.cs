namespace Pica.Desktop.Services.FileAssociations;

internal interface IPicaFileAssociationStore
{
    string? GetDefaultProgram(string extension);
    string? GetFallbackProgram(string extension);
    bool IsPicaDefault(string extension);
    bool IsPicaProgram(string? programId);
    FileAssociationSnapshot GetSnapshot(string extension);
    void ValidateUserChoice(string extension);
    void SetUserChoice(string extension, string? programId);
    void ClearPicaFallbacks(string extension);
    void RestoreSnapshot(string extension, FileAssociationSnapshot snapshot);
    void RegisterApplication(IReadOnlyList<string> extensions);
    void NotifyChanged();
}

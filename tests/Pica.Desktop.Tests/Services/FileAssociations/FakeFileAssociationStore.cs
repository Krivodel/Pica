using Pica.Desktop.Services.FileAssociations;

namespace Pica.Desktop.Tests.Services.FileAssociations;

internal sealed class FakeFileAssociationStore : IPicaFileAssociationStore
{
    internal Dictionary<string, string?> Choices { get; } = [];
    internal Dictionary<string, string?> InheritedDefaults { get; } = [];
    internal Dictionary<string, string?> UserClassPrograms { get; } = [];
    internal HashSet<string> PicaProgramIds { get; } = [PicaFileAssociationService.ProgramId];
    internal List<string> Writes { get; } = [];
    internal string? InvalidExtension { get; set; }
    internal string? FailingExtension { get; set; }
    internal string? IgnoredExtension { get; set; }
    internal Action<string>? AfterWrite { get; set; }
    internal IReadOnlyList<string>? RegisteredExtensions { get; private set; }

    public string? GetDefaultProgram(string extension)
    {
        return Choices.GetValueOrDefault(extension) ?? UserClassPrograms.GetValueOrDefault(extension)
            ?? InheritedDefaults.GetValueOrDefault(extension);
    }

    public FileAssociationSnapshot GetSnapshot(string extension)
    {
        return new FileAssociationSnapshot(GetUserChoice(extension), GetDefaultProgram(extension), GetUserClassProgram(extension));
    }

    public string? GetUserChoice(string extension)
    {
        return Choices.GetValueOrDefault(extension);
    }

    public bool IsPicaDefault(string extension)
    {
        return IsPicaProgram(GetDefaultProgram(extension));
    }

    public bool IsPicaProgram(string? programId)
    {
        return programId is not null && PicaProgramIds.Contains(programId);
    }

    public string? GetUserClassProgram(string extension)
    {
        return UserClassPrograms.GetValueOrDefault(extension);
    }

    public string? GetFallbackProgram(string extension)
    {
        string? program = GetUserClassProgram(extension);

        if (program is null || IsPicaProgram(program))
        {
            program = InheritedDefaults.GetValueOrDefault(extension);
        }

        return IsPicaProgram(program) ? null : program;
    }

    public void ClearPicaFallbacks(string extension)
    {
        if (IsPicaProgram(GetUserClassProgram(extension)))
        {
            UserClassPrograms[extension] = null;
        }
    }

    public void RestoreSnapshot(string extension, FileAssociationSnapshot snapshot)
    {
        if (snapshot.Matches(GetSnapshot(extension)))
        {
            return;
        }

        UserClassPrograms[extension] = snapshot.UserClassProgram;
        SetUserChoice(extension, snapshot.UserChoice);

        if (!snapshot.Matches(GetSnapshot(extension)))
        {
            throw new IOException($"Test default application restoration failed for '{extension}'.");
        }
    }

    public void ValidateUserChoice(string extension)
    {
        if (extension == InvalidExtension)
        {
            throw new NotSupportedException("Test incompatible Windows record");
        }
    }

    public void SetUserChoice(string extension, string? programId)
    {
        Writes.Add(extension);

        if (extension == FailingExtension)
        {
            throw new IOException("Test registry access denied");
        }

        if (extension == IgnoredExtension)
        {
            return;
        }

        Choices[extension] = programId;
        AfterWrite?.Invoke(extension);
    }

    public void RegisterApplication(IReadOnlyList<string> extensions)
    {
        RegisteredExtensions = extensions.ToArray();
    }

    public void NotifyChanged()
    {
    }
}

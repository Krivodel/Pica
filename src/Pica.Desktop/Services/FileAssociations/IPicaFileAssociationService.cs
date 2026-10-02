namespace Pica.Desktop.Services.FileAssociations;

internal interface IPicaFileAssociationService
{
    IReadOnlyList<string> SupportedExtensions { get; }

    Task RegisterAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> LoadSelectionAsync(CancellationToken ct);
    Task ApplyAsync(IReadOnlyList<string> extensions, CancellationToken ct);
    Task DismissPromptAsync(CancellationToken ct);
}

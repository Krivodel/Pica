using Pica.Desktop.Services.FileAssociations;

namespace Pica.Desktop.Tests.ViewModels;

internal sealed class FakeFileAssociationService : IPicaFileAssociationService
{
    public IReadOnlyList<string> SupportedExtensions { get; init; } = Array.AsReadOnly(new string[] { ".jpg", ".png", ".webp" });

    internal IReadOnlyList<string> Selected { get; set; } = new string[] { ".png" };
    internal Exception? Failure { get; set; }
    internal Func<CancellationToken, Task>? Applying { get; set; }
    internal Func<CancellationToken, Task>? Dismissing { get; set; }

    public Task RegisterAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> LoadSelectionAsync(CancellationToken ct)
    {
        return Failure is null ? Task.FromResult(Selected) : Task.FromException<IReadOnlyList<string>>(Failure);
    }

    public async Task ApplyAsync(IReadOnlyList<string> extensions, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Applying is not null)
        {
            await Applying(ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        Selected = extensions.ToArray();
    }

    public Task DismissPromptAsync(CancellationToken ct)
    {
        return Dismissing?.Invoke(ct) ?? Task.CompletedTask;
    }
}

using Avalonia.Controls;

namespace Pica.Desktop.Services;

internal interface IPicaStartupPrompt
{
    Task Completion { get; }

    Task ShowIfNeededAsync(Window owner, CancellationToken ct);
}

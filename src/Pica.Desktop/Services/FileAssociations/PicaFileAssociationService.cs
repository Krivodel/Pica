using Pica.Viewer.Services;

namespace Pica.Desktop.Services.FileAssociations;

internal sealed class PicaFileAssociationService : IPicaFileAssociationService
{
    public IReadOnlyList<string> SupportedExtensions { get; }

    internal const string ProgramId = "Pica.Image";

    private readonly IPicaFileAssociationStore _store;
    private readonly IPicaDesktopStateService _stateService;

    public PicaFileAssociationService(
        IImageFormatRegistry formatRegistry,
        IPicaFileAssociationStore store,
        IPicaDesktopStateService stateService)
    {
        ArgumentNullException.ThrowIfNull(formatRegistry);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        SupportedExtensions = Array.AsReadOnly(formatRegistry.GetSupportedExtensions()
            .Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public Task RegisterAsync(CancellationToken ct)
    {
        return Task.Run(() => _store.RegisterApplication(SupportedExtensions), ct);
    }

    public async Task<IReadOnlyList<string>> LoadSelectionAsync(CancellationToken ct)
    {
        PicaDesktopState state = await _stateService.LoadAsync(ct).ConfigureAwait(false);

        return await Task.Run(() =>
        {
            return state.HasSeenFileAssociationsPrompt
                ? SupportedExtensions.Where(_store.IsPicaDefault).ToArray()
                : SupportedExtensions.ToArray();
        }, ct).ConfigureAwait(false);
    }

    public async Task ApplyAsync(IReadOnlyList<string> extensions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        HashSet<string> selected = new(extensions, StringComparer.OrdinalIgnoreCase);

        if (selected.Any(extension => !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The file association selection includes an unsupported extension.", nameof(extensions));
        }

        Dictionary<string, FileAssociationSnapshot> snapshots = new(StringComparer.OrdinalIgnoreCase);
        List<string> changed = [];

        await _stateService.UpdateAsync(async (state, token) =>
        {
            await Task.Run(() =>
            {
                string[] targets = SupportedExtensions
                    .Where(extension => selected.Contains(extension) != _store.IsPicaDefault(extension))
                    .ToArray();

                foreach (string extension in targets)
                {
                    token.ThrowIfCancellationRequested();
                    _store.ValidateUserChoice(extension);
                    snapshots.Add(extension, _store.GetSnapshot(extension));
                }

                foreach (string extension in targets)
                {
                    token.ThrowIfCancellationRequested();
                    bool enable = selected.Contains(extension);
                    FileAssociationSnapshot snapshot = snapshots[extension];

                    if (!snapshot.Matches(_store.GetSnapshot(extension)))
                    {
                        throw new IOException($"The default application for '{extension}' changed during preparation.");
                    }

                    if (enable)
                    {
                        state.PreviousFileAssociations[extension] = snapshot.DefaultProgram;
                    }

                    string? target = enable ? ProgramId : state.PreviousFileAssociations.GetValueOrDefault(extension);

                    if (!enable && (target is null || _store.IsPicaProgram(target)))
                    {
                        target = _store.GetFallbackProgram(extension);
                    }

                    changed.Add(extension);

                    if (!enable)
                    {
                        _store.ClearPicaFallbacks(extension);
                    }

                    _store.SetUserChoice(extension, target);
                    _store.NotifyChanged();
                    string? actualProgram = _store.GetDefaultProgram(extension);

                    if ((target is not null && !string.Equals(actualProgram, target, StringComparison.OrdinalIgnoreCase))
                        || (target is null && _store.IsPicaDefault(extension)))
                    {
                        throw new NotSupportedException($"Windows did not accept the default application change for '{extension}': requested '{target}', resolved '{actualProgram}'.");
                    }
                }

                foreach (string extension in SupportedExtensions.Where(extension => !selected.Contains(extension)))
                {
                    state.PreviousFileAssociations.Remove(extension);
                }

                state.HasSeenFileAssociationsPrompt = true;
            }, token).ConfigureAwait(false);
        }, () => Task.Run(() =>
        {
            List<Exception> failures = [];

            foreach (string extension in changed.AsEnumerable().Reverse())
            {
                try
                {
                    _store.RestoreSnapshot(extension, snapshots[extension]);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            _store.NotifyChanged();

            if (failures.Count > 0)
            {
                throw new AggregateException("Pica could not restore all previous file associations.", failures);
            }
        }), ct).ConfigureAwait(false);
    }

    public Task DismissPromptAsync(CancellationToken ct)
    {
        return _stateService.UpdateAsync(state => state.HasSeenFileAssociationsPrompt = true, ct);
    }
}

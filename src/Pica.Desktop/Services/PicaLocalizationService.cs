using Microsoft.Extensions.Logging;

using Krivodeling.Localization.Avalonia;
using Pica.Desktop.Resources;
using Pica.Protocol;

namespace Pica.Desktop.Services;

internal sealed class PicaLocalizationService : IDisposable
{
    internal LocalizationService Localization { get; }

    private readonly IPicaDesktopStateService _stateService;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _selectionLock = new(1, 1);
    private bool _isInitialized;

    public PicaLocalizationService(IPicaDesktopStateService stateService, ILogger<LocalizationService> logger)
        : this(stateService, new DirectoryLocalizationFileStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PicaProtocolConstants.ApplicationName, "Localizations")), logger)
    {
    }

    internal PicaLocalizationService(IPicaDesktopStateService stateService, LocalizationFileStore fileStore,
        ILogger<LocalizationService> logger)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        Localization = new LocalizationService(fileStore, DesktopLocalization.Catalog, logger);
    }

    internal async Task InitializeAsync(CancellationToken ct)
    {
        await _initializationLock.WaitAsync(ct);

        try
        {
            if (_isInitialized)
            {
                return;
            }

            await Localization.RefreshAvailableLocalizationsAsync(ct);
            PicaDesktopState state = await _stateService.LoadAsync(ct);

            if (string.IsNullOrWhiteSpace(state.LocalizationId))
            {
                Localization.ReconcileCurrentOrSystemDefault();
            }
            else
            {
                Localization.SelectSavedOrEnglishFallback(state.LocalizationId);
            }

            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    internal async Task SelectAsync(string id, CancellationToken ct)
    {
        await _selectionLock.WaitAsync(ct);
        string previous = Localization.CurrentLocalization?.Id ?? LocalizationConstants.EnglishId;

        try
        {
            Localization.Select(id);

            try
            {
                await _stateService.UpdateAsync(state => state.LocalizationId = id, ct);
            }
            catch (Exception)
            {
                Localization.SelectSavedOrEnglishFallback(previous);
                throw;
            }
        }
        finally
        {
            _selectionLock.Release();
        }
    }

    public void Dispose()
    {
        Localization.Dispose();
        _initializationLock.Dispose();
        _selectionLock.Dispose();
    }
}

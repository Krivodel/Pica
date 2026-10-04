using Krivodeling.Localization.Avalonia;
using Pica.Viewer.Resources;

namespace Pica.Desktop.Resources;

internal static class DesktopLocalization
{
    internal static BuiltInLocalizationCatalog Catalog { get; } = BuiltInLocalizationCatalog.FromAssemblies(
        typeof(ViewerLocalization).Assembly, typeof(DesktopLocalization).Assembly);

    internal static string Get(string key)
    {
        LocalizationText.InitializeIfNeeded(Catalog);

        return LocalizationText.Get(key);
    }
}

using Krivodeling.Localization.Avalonia;

namespace Pica.Viewer.Resources;

public static class ViewerLocalization
{
    public static BuiltInLocalizationCatalog Catalog { get; } =
        BuiltInLocalizationCatalog.FromAssemblies(typeof(ViewerLocalization).Assembly);

    public static string Get(string key)
    {
        LocalizationText.InitializeIfNeeded(Catalog);

        return LocalizationText.Get(key);
    }
}

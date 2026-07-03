using System.Collections;
using System.Globalization;
using System.Resources;

namespace ChapterTool.Avalonia.Localization;

public static class AppLocalizationResources
{
    private static readonly ResourceManager ResourceManager = new(
        "ChapterTool.Avalonia.Localization.Resources.Strings",
        typeof(AppLocalizationResources).Assembly);

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } =
        AppLanguage.Supported.ToDictionary(
            static language => language.CultureName,
            static language => LoadCulture(language.CultureName),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Fallback { get; } = All[AppLanguage.DefaultCultureName];

    private static IReadOnlyDictionary<string, string> LoadCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var resourceSet = ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new MissingManifestResourceException($"Localization resources for '{cultureName}' were not found.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                values[key] = value;
            }
        }

        return values;
    }
}

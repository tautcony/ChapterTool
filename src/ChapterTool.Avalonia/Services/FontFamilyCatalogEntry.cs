using System.Globalization;

namespace ChapterTool.Avalonia.Services;

public sealed class FontFamilyCatalogEntry
{
    private readonly Lazy<IReadOnlyDictionary<string, string>> localizedNames;

    public FontFamilyCatalogEntry(
        string familyName,
        IReadOnlyDictionary<string, string>? localizedNames = null)
        : this(familyName, () => localizedNames ?? new Dictionary<string, string>())
    {
    }

    internal FontFamilyCatalogEntry(
        string familyName,
        Func<IReadOnlyDictionary<string, string>> localizedNamesFactory)
    {
        FamilyName = familyName;
        localizedNames = new Lazy<IReadOnlyDictionary<string, string>>(
            localizedNamesFactory,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string FamilyName { get; }

    public string GetDisplayName(string? cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(
            string.IsNullOrWhiteSpace(cultureName) ? CultureInfo.CurrentUICulture.Name : cultureName);
        var names = localizedNames.Value;
        if (names.TryGetValue(culture.Name, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var sameLanguage = names
            .Where(entry => SameLanguage(entry.Key, culture))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static entry => entry.Value)
            .FirstOrDefault(static name => !string.IsNullOrWhiteSpace(name));
        return sameLanguage ?? FamilyName;
    }

    private static bool SameLanguage(string cultureName, CultureInfo targetCulture)
    {
        try
        {
            return string.Equals(
                CultureInfo.GetCultureInfo(cultureName).TwoLetterISOLanguageName,
                targetCulture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}

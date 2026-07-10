using System.Globalization;
using ChapterTool.Infrastructure.Configuration;

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

public interface IFontFamilyCatalog
{
    IReadOnlyList<FontFamilyCatalogEntry> Families { get; }

    bool TryResolve(string? familyName, out string resolvedFamilyName);
}

public sealed class AvaloniaFontFamilyCatalog : IFontFamilyCatalog
{
    private readonly Dictionary<string, string> canonicalNames;

    public AvaloniaFontFamilyCatalog()
        : this(CreateSystemEntries(), CultureInfo.CurrentUICulture, entriesAreCanonical: true)
    {
    }

    public AvaloniaFontFamilyCatalog(IEnumerable<string?> familyNames, CultureInfo? culture = null)
        : this(
            CreateEntries(familyNames),
            culture ?? CultureInfo.CurrentUICulture,
            entriesAreCanonical: true)
    {
    }

    private AvaloniaFontFamilyCatalog(
        IEnumerable<FontFamilyCatalogEntry> entries,
        CultureInfo culture,
        bool entriesAreCanonical)
    {
        _ = entriesAreCanonical;
        var canonicalEntries = entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.FamilyName))
            .DistinctBy(static entry => entry.FamilyName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry.FamilyName, StringComparer.Create(culture, ignoreCase: true))
            .ThenBy(static entry => entry.FamilyName, StringComparer.Ordinal)
            .ToArray();

        Families = canonicalEntries;
        canonicalNames = canonicalEntries.ToDictionary(static entry => entry.FamilyName, static entry => entry.FamilyName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FontFamilyCatalogEntry> Families { get; }

    public static AvaloniaFontFamilyCatalog FromEntries(
        IEnumerable<FontFamilyCatalogEntry> entries,
        CultureInfo? culture = null) =>
        new(entries, culture ?? CultureInfo.CurrentUICulture, entriesAreCanonical: true);

    public bool TryResolve(string? familyName, out string resolvedFamilyName)
    {
        if (!string.IsNullOrWhiteSpace(familyName)
            && canonicalNames.TryGetValue(familyName.Trim(), out var canonicalName))
        {
            resolvedFamilyName = canonicalName;
            return true;
        }

        resolvedFamilyName = string.Empty;
        return false;
    }

    private static IEnumerable<FontFamilyCatalogEntry> CreateSystemEntries()
    {
        foreach (var family in global::Avalonia.Media.FontManager.Current.SystemFonts)
        {
            var capturedFamily = family;
            yield return new FontFamilyCatalogEntry(family.Name, () => ReadLocalizedNames(capturedFamily));
        }
    }

    private static IEnumerable<FontFamilyCatalogEntry> CreateEntries(IEnumerable<string?> familyNames)
    {
        ArgumentNullException.ThrowIfNull(familyNames);
        return familyNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => new FontFamilyCatalogEntry(name!.Trim()));
    }

    private static IReadOnlyDictionary<string, string> ReadLocalizedNames(global::Avalonia.Media.FontFamily family)
    {
        if (!global::Avalonia.Media.FontManager.Current.TryGetGlyphTypeface(
                new global::Avalonia.Media.Typeface(family),
                out var glyphTypeface))
        {
            return new Dictionary<string, string>();
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (culture, name) in glyphTypeface.FamilyNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            names[culture.Name] = name.Trim();
        }

        return names;
    }
}

public static class FontSettingsResolver
{
    public static FontSettings Resolve(FontSettings? settings, IFontFamilyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var normalized = FontSettings.Normalize(settings);
        return new FontSettings(
            ResolveFamily(normalized.UiFontFamily, catalog),
            ResolveFamily(normalized.MonospaceFontFamily, catalog));
    }

    private static string ResolveFamily(string familyName, IFontFamilyCatalog catalog) =>
        catalog.TryResolve(familyName, out var resolved) ? resolved : string.Empty;
}

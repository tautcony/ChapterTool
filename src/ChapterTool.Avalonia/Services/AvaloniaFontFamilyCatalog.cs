using System.Globalization;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaFontFamilyCatalog : IFontFamilyCatalog
{
    private readonly Dictionary<string, string> canonicalNames;

    public AvaloniaFontFamilyCatalog()
        : this(CreateSystemEntries(), CultureInfo.CurrentUICulture)
    {
    }

    public AvaloniaFontFamilyCatalog(IEnumerable<string?> familyNames, CultureInfo? culture = null)
        : this(CreateEntries(familyNames), culture ?? CultureInfo.CurrentUICulture)
    {
    }

    private AvaloniaFontFamilyCatalog(
        IEnumerable<FontFamilyCatalogEntry> entries,
        CultureInfo culture)
    {
        var canonicalEntries = entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.FamilyName))
            .DistinctBy(static entry => entry.FamilyName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry.FamilyName, StringComparer.Create(culture, ignoreCase: true))
            .ThenBy(static entry => entry.FamilyName, StringComparer.Ordinal)
            .ToArray();

        Families = canonicalEntries;
        canonicalNames = canonicalEntries.ToDictionary(
            static entry => entry.FamilyName,
            static entry => entry.FamilyName,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FontFamilyCatalogEntry> Families { get; }

    public static AvaloniaFontFamilyCatalog FromEntries(
        IEnumerable<FontFamilyCatalogEntry> entries,
        CultureInfo? culture = null) =>
        new(entries, culture ?? CultureInfo.CurrentUICulture);

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

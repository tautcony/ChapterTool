using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

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

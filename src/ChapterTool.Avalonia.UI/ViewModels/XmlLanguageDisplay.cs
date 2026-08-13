using System.Collections.Concurrent;
using System.Globalization;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.UI.ViewModels;

internal static class XmlLanguageDisplay
{
    private static readonly ConcurrentDictionary<(string CultureName, string LanguageCode), string> DisplayNameCache = new();

    public static IReadOnlyList<SelectorDisplayOption> Options(IAppLocalizer localizer) =>
    [
        .. XmlChapterLanguageCatalog.Languages
            .Select(language =>
            {
                var displayName = LanguageDisplayName(language, localizer);
                return new SelectorDisplayOption(language.Code, displayName, $"{language.Code}（{displayName}）");
            })
    ];

    private static string LanguageDisplayName(XmlChapterLanguage language, IAppLocalizer localizer)
    {
        if (language.Code.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            return localizer.GetString("XmlLanguage.Undetermined");
        }

        try
        {
            var culture = CultureForCode(language.Code);
            if (culture is null)
            {
                return EnglishDisplayName(language);
            }

            var cacheKey = (localizer.CurrentCultureName, language.Code);
            return DisplayNameCache.GetOrAdd(cacheKey, key => DisplayNameIn(culture, key.CultureName));
        }
        catch (CultureNotFoundException)
        {
            return EnglishDisplayName(language);
        }
    }

    // CultureInfo.DisplayName renders in the ambient thread UI culture. Resolve
    // against the localizer culture instead, so the label follows the app language
    // deterministically regardless of the machine locale or thread state.
    private static string DisplayNameIn(CultureInfo culture, string uiCultureName)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCultureName);
            return culture.DisplayName;
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static CultureInfo? CultureForCode(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultures(CultureTypes.NeutralCultures)
                .FirstOrDefault(culture =>
                    string.Equals(culture.ThreeLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(culture.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string EnglishDisplayName(XmlChapterLanguage language)
    {
        const string separator = " - ";
        var separatorIndex = language.DisplayName.IndexOf(separator, StringComparison.Ordinal);
        return separatorIndex >= 0
            ? language.DisplayName[(separatorIndex + separator.Length)..]
            : language.DisplayName;
    }
}

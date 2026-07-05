using System.Globalization;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.ViewModels;

internal static class XmlLanguageDisplay
{
    public static IReadOnlyList<SelectorDisplayOption> Options(IAppLocalizer localizer) =>
        XmlChapterLanguageCatalog.Languages
            .Select(language =>
            {
                var displayName = LanguageDisplayName(language, localizer);
                return new SelectorDisplayOption(language.Code, displayName, $"{language.Code}（{displayName}）");
            })
            .ToArray();

    private static string LanguageDisplayName(XmlChapterLanguage language, IAppLocalizer localizer)
    {
        if (language.Code.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            return localizer.GetString("XmlLanguage.Undetermined");
        }

        try
        {
            using var _ = new TemporaryCurrentUiCulture(localizer.CurrentCultureName);
            var culture = CultureForCode(language.Code);
            return culture is null
                ? EnglishDisplayName(language)
                : culture.DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return EnglishDisplayName(language);
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

    private sealed class TemporaryCurrentUiCulture : IDisposable
    {
        private readonly CultureInfo previousCulture;
        private readonly CultureInfo previousUiCulture;

        public TemporaryCurrentUiCulture(string cultureName)
        {
            previousCulture = CultureInfo.CurrentCulture;
            previousUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}

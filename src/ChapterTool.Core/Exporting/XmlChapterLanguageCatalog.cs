using System.Globalization;

namespace ChapterTool.Core.Exporting;

public sealed record XmlChapterLanguage(string Code, string DisplayName);

public static class XmlChapterLanguageCatalog
{
    private static readonly string[] QuickCodes = ["und", "zh", "ja", "en", "jpn"];

    public static IReadOnlyList<XmlChapterLanguage> Languages { get; } = BuildLanguages();

    public static bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Languages.Any(language => string.Equals(language.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeOrDefault(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "und";
        }

        var trimmed = code.Trim();
        return IsValidCode(trimmed) ? trimmed.ToLowerInvariant() : "und";
    }

    private static IReadOnlyList<XmlChapterLanguage> BuildLanguages()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .SelectMany(static culture => new[]
            {
                new XmlChapterLanguage(culture.TwoLetterISOLanguageName, $"{culture.TwoLetterISOLanguageName} - {culture.EnglishName}"),
                new XmlChapterLanguage(culture.ThreeLetterISOLanguageName, $"{culture.ThreeLetterISOLanguageName} - {culture.EnglishName}")
            })
            .Where(static language => (language.Code.Length == 2 || language.Code.Length == 3) && language.Code != "iv" && language.Code != "ivl")
            .GroupBy(static language => language.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static language => language.Code, StringComparer.OrdinalIgnoreCase);

        return QuickCodes
            .Select(static code => new XmlChapterLanguage(code, DisplayNameFor(code)))
            .Concat(cultures.Where(static language => !QuickCodes.Contains(language.Code, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string DisplayNameFor(string code) =>
        code switch
        {
            "und" => "und - Undetermined",
            "jpn" => "jpn - Japanese",
            _ => $"{code} - {new CultureInfo(code).EnglishName}"
        };
}

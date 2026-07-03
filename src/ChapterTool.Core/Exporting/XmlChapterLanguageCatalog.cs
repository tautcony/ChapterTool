using System.Globalization;

namespace ChapterTool.Core.Exporting;

public sealed record XmlChapterLanguage(string Code, string DisplayName);

public static class XmlChapterLanguageCatalog
{
    private static readonly string[] QuickCodes = ["und", "zh", "ja", "en", "jpn"];

    public static IReadOnlyList<XmlChapterLanguage> Languages { get; } = BuildLanguages();

    private static readonly HashSet<string> LanguageCodes = Languages
        .Select(static language => language.Code)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return LanguageCodes.Contains(code.Trim());
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

    private static List<XmlChapterLanguage> BuildLanguages()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .SelectMany(static culture => new[]
            {
                new XmlChapterLanguage(culture.TwoLetterISOLanguageName, $"{culture.TwoLetterISOLanguageName} - {culture.EnglishName}"),
                new XmlChapterLanguage(culture.ThreeLetterISOLanguageName, $"{culture.ThreeLetterISOLanguageName} - {culture.EnglishName}")
            })
            .Where(static language => language.Code.Length is 2 or 3 && language.Code != "iv" && language.Code != "ivl")
            .GroupBy(static language => language.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static language => language.Code, StringComparer.OrdinalIgnoreCase);

        return QuickCodes
            .Select(static code => new XmlChapterLanguage(code, DisplayNameFor(code)))
            .Concat(cultures.Where(static language => !QuickCodes.Contains(language.Code, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string DisplayNameFor(string code)
    {
        try
        {
            return code switch
            {
                "und" => "und - Undetermined",
                "jpn" => "jpn - Japanese",
                _ => $"{code} - {new CultureInfo(code).EnglishName}"
            };
        }
        catch (CultureNotFoundException)
        {
            return $"{code} - Unknown";
        }
    }
}

namespace ChapterTool.Avalonia.Localization;

public sealed record AppLanguage(string CultureName, string DisplayNameKey)
{
    public static string DefaultCultureName => "zh-CN";

    public static IReadOnlyList<AppLanguage> Supported { get; } =
    [
        new("zh-CN", "Language.ChineseSimplified"),
        new("en-US", "Language.English"),
        new("ja-JP", "Language.Japanese")
    ];

    public static string Normalize(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCultureName;
        }

        return Supported.Any(language => string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase))
            ? Supported.First(language => string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase)).CultureName
            : DefaultCultureName;
    }
}

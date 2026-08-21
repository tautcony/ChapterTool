namespace ChapterTool.Avalonia.UI.Localization;

public sealed record AppLanguage(string CultureName, string DisplayNameKey, string NativeDisplayName)
{
    public static string DefaultCultureName => "zh-CN";

    public static IReadOnlyList<AppLanguage> Supported { get; } =
    [
        new("zh-CN", "Language.ChineseSimplified", "简体中文"),
        new("en-US", "Language.English", "English"),
        new("ja-JP", "Language.Japanese", "日本語")
    ];

    public static string Normalize(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCultureName;
        }

        return Supported.FirstOrDefault(language =>
                   string.Equals(language.CultureName, cultureName.Trim(), StringComparison.OrdinalIgnoreCase))
               ?.CultureName
            ?? DefaultCultureName;
    }
}

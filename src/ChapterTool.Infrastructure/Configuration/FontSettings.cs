namespace ChapterTool.Infrastructure.Configuration;

public sealed record FontSettings(
    string UiFontFamily = "",
    string MonospaceFontFamily = "")
{
    public static FontSettings Default { get; } = new();

    public static FontSettings Normalize(FontSettings? settings) =>
        settings is null
            ? Default
            : new FontSettings(NormalizeFamily(settings.UiFontFamily), NormalizeFamily(settings.MonospaceFontFamily));

    private static string NormalizeFamily(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

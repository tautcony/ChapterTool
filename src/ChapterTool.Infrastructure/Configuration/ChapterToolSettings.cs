namespace ChapterTool.Infrastructure.Configuration;

public sealed record ChapterToolSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public AppSettings Application { get; init; } = new();

    public ThemeSettings Theme { get; init; } = ThemeSettings.Default;

    public FontSettings Font { get; init; } = FontSettings.Default;

    public static ChapterToolSettings Default { get; } = new();

    public static ChapterToolSettings Normalize(ChapterToolSettings? settings) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            Application = settings?.Application ?? new AppSettings(),
            Theme = ThemePresetCatalog.Normalize(settings?.Theme),
            Font = FontSettings.Normalize(settings?.Font),
        };
}

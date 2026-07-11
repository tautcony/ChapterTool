using ChapterTool.Core.Exporting;

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
            Application = NormalizeApplication(settings?.Application),
            Theme = ThemePresetCatalog.Normalize(settings?.Theme),
            Font = FontSettings.Normalize(settings?.Font),
        };

    private static AppSettings NormalizeApplication(AppSettings? settings)
    {
        settings ??= new AppSettings();
        return settings with
        {
            SavingPath = NormalizeDirectory(settings.SavingPath),
            OutputTextEncoding = OutputTextEncodings.Id(OutputTextEncodings.ParseOrDefault(settings.OutputTextEncoding))
        };
    }

    private static string? NormalizeDirectory(string? path) => ChapterSavePath.CleanOptionalPath(path);
}

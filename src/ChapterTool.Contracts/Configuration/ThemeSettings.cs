namespace ChapterTool.Contracts.Configuration;

public sealed record ThemeSettings(string PresetId)
{
    public static ThemeSettings Default { get; } = new(ThemePresetCatalog.DefaultPresetId);
}

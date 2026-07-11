namespace ChapterTool.Infrastructure.Configuration;

public enum ThemeBaseVariant
{
    Light,
    Dark
}

public sealed record ThemePalette(
    string WindowBackground,
    string PanelBackground,
    string ControlBackground,
    string ControlForeground,
    string MutedForeground,
    string Accent,
    string AccentForeground,
    string Border,
    string HoverBackground,
    string ActiveBackground,
    string FrameNeutral,
    string FrameAccurate,
    string FrameInexact,
    string DiagnosticError)
{
    public IReadOnlyList<string> PreviewSwatches =>
    [
        WindowBackground,
        PanelBackground,
        ControlBackground,
        ControlForeground,
        Accent,
        Border,
        HoverBackground,
        ActiveBackground
    ];
}

public sealed record ThemePreset(
    string Id,
    string Name,
    string DisplayNameKey,
    ThemeBaseVariant BaseVariant,
    ThemePalette Palette);

public static class ThemePresetCatalog
{
    public const string DefaultPresetId = "avalonia-default";

    public static IReadOnlyList<ThemePreset> All { get; } =
    [
        Preset(DefaultPresetId, "Avalonia Default", ThemeBaseVariant.Light,
            "#F0F0F0", "#F7F7F7", "#FFFFFF", "#111111", "#595959", "#0067C0", "#FFFFFF", "#767676", "#D6E9F8", "#CCE4F7"),
        Preset("solarized-light", "Solarized Light", ThemeBaseVariant.Light,
            "#FDF6E3", "#EEE8D5", "#FFFDF5", "#002B36", "#586E75", "#006C75", "#FFFFFF", "#657B83", "#E1DABF", "#C8BE9F"),
        Preset("solarized-dark", "Solarized Dark", ThemeBaseVariant.Dark,
            "#002B36", "#073642", "#0B414D", "#FDF6E3", "#93A1A1", "#D9A400", "#002B36", "#839496", "#155766", "#1E6675"),
        Preset("gruvbox-light", "Gruvbox Light", ThemeBaseVariant.Light,
            "#FBF1C7", "#EBDBB2", "#FFF8D9", "#282828", "#665C54", "#9D0006", "#FFFFFF", "#7C6F64", "#D5C4A1", "#BDAE93"),
        Preset("gruvbox-dark", "Gruvbox Dark", ThemeBaseVariant.Dark,
            "#282828", "#3C3836", "#32302F", "#FBF1C7", "#BDAE93", "#D79921", "#1D2021", "#A89984", "#504945", "#665C54"),
        Preset("ayu-light", "Ayu Light", ThemeBaseVariant.Light,
            "#FAFAFA", "#F3F4F5", "#FFFFFF", "#242936", "#5C6773", "#006D77", "#FFFFFF", "#6C7680", "#E1E7EA", "#CBD3D8"),
        Preset("ayu-mirage", "Ayu Mirage", ThemeBaseVariant.Dark,
            "#1F2430", "#242936", "#2A3141", "#F3F4F5", "#A6ACB9", "#FFCC66", "#1F2430", "#8A919F", "#343D50", "#475268"),
        Preset("ayu-dark", "Ayu Dark", ThemeBaseVariant.Dark,
            "#0B0E14", "#11151C", "#151A23", "#E6E1CF", "#9DA5B4", "#FFB454", "#0B0E14", "#7A8494", "#242B38", "#343D4D")
    ];

    public static ThemePreset Default => All[0];

    public static ThemePreset Resolve(string? presetId) =>
        All.FirstOrDefault(preset => string.Equals(preset.Id, presetId, StringComparison.OrdinalIgnoreCase)) ?? Default;

    public static ThemeSettings Normalize(ThemeSettings? settings) => new(Resolve(settings?.PresetId).Id);

    private static ThemePreset Preset(
        string id,
        string name,
        ThemeBaseVariant baseVariant,
        string windowBackground,
        string panelBackground,
        string controlBackground,
        string controlForeground,
        string mutedForeground,
        string accent,
        string accentForeground,
        string border,
        string hoverBackground,
        string activeBackground)
    {
        var dark = baseVariant == ThemeBaseVariant.Dark;
        return new(
            id,
            name,
            $"Settings.Appearance.Preset.{id}",
            baseVariant,
            new ThemePalette(
                windowBackground,
                panelBackground,
                controlBackground,
                controlForeground,
                mutedForeground,
                accent,
                accentForeground,
                border,
                hoverBackground,
                activeBackground,
                FrameNeutral: dark ? "#F3F4F5" : "#111111",
                FrameAccurate: dark ? "#6EE7A0" : "#0F7A2F",
                FrameInexact: dark ? "#FF8A80" : "#B42318",
                DiagnosticError: dark ? "#FF8A80" : "#B42318"));
    }
}

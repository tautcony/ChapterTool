using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaThemeApplicationService : IThemeApplicationService
{
    public static IReadOnlyList<string> ImportedThemeColorKeys { get; } =
    [
        "Color.Window", "Color.WindowBorder", "Color.ToolBar", "Color.Popup",
        "Color.PopupBorder", "Color.Contents", "Color.Border1", "Color.Border2",
        "Color.FlatButton.Background", "Color.FlatButton.BackgroundHovered",
        "Color.FG1", "Color.FG2", "Color.Hover", "Color.Active", "Color.Selection"
    ];

    public const string FrameNeutralBrushKey = "ChapterTool.FrameNeutralBrush";
    public const string FrameAccurateBrushKey = "ChapterTool.FrameAccurateBrush";
    public const string FrameInexactBrushKey = "ChapterTool.FrameInexactBrush";
    public const string DiagnosticErrorBrushKey = "ChapterTool.DiagnosticErrorBrush";
    public const string LogInformationBrushKey = "ChapterTool.LogInformationBrush";
    public const string LogWarningBrushKey = "ChapterTool.LogWarningBrush";
    public const string LogErrorBrushKey = "ChapterTool.LogErrorBrush";

    public void Apply(ThemeSettings settings)
    {
        var application = Application.Current;
        if (application?.Resources is null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Apply(settings));
            return;
        }

        var preset = ThemePresetCatalog.Resolve(settings.PresetId);
        var palette = preset.Palette;
        var resources = application.Resources;
        resources[FrameNeutralBrushKey] = Brush(palette.FrameNeutral);
        resources[FrameAccurateBrushKey] = Brush(palette.FrameAccurate);
        resources[FrameInexactBrushKey] = Brush(palette.FrameInexact);
        resources[DiagnosticErrorBrushKey] = Brush(palette.DiagnosticError);
        resources[LogInformationBrushKey] = Brush(palette.Accent);
        resources[LogWarningBrushKey] = Brush(preset.BaseVariant == ThemeBaseVariant.Dark ? "#FFD580" : "#A15C00");
        resources[LogErrorBrushKey] = Brush(palette.DiagnosticError);

        foreach (var (key, value) in ComputeImportedThemeColors(preset))
        {
            resources[key] = Color.Parse(value);
        }

        application.RequestedThemeVariant = preset.BaseVariant == ThemeBaseVariant.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    internal static IReadOnlyDictionary<string, string> ComputeImportedThemeColors(ThemePreset preset)
    {
        var palette = preset.Palette;
        var auxiliary = AuxiliaryPalette(preset, palette);
        var dark = preset.BaseVariant == ThemeBaseVariant.Dark;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Color.Window"] = palette.WindowBackground,
            ["Color.WindowBorder"] = palette.Border,
            ["Color.ToolBar"] = auxiliary.Toolbar,
            ["Color.Popup"] = auxiliary.Popup,
            ["Color.PopupBorder"] = dark ? auxiliary.SubtleBorder : palette.ControlBackground,
            ["Color.Contents"] = auxiliary.Content,
            ["Color.Border1"] = palette.Border,
            ["Color.Border2"] = auxiliary.SubtleBorder,
            ["Color.FlatButton.Background"] = auxiliary.Control,
            ["Color.FlatButton.BackgroundHovered"] = auxiliary.Hover,
            ["Color.FG1"] = palette.ControlForeground,
            ["Color.FG2"] = palette.MutedForeground,
            ["Color.Hover"] = palette.HoverBackground,
            ["Color.Active"] = palette.ActiveBackground,
            ["Color.Selection"] = auxiliary.Selection
        };
    }

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));

    private static AuxiliaryThemePalette AuxiliaryPalette(ThemePreset preset, ThemePalette palette)
    {
        if (string.Equals(preset.Id, ThemePresetCatalog.DefaultPresetId, StringComparison.Ordinal))
        {
            return new(
                Toolbar: "#F0F5F9",
                Content: "#FAFAFA",
                Control: "#F8F8F8",
                Popup: "#F4F8FB",
                SubtleBorder: "#CFCFCF",
                Hover: "#FFFFFF",
                Selection: "#D7E7F3");
        }

        if (preset.BaseVariant == ThemeBaseVariant.Dark)
        {
            return new(
                Toolbar: palette.PanelBackground,
                Content: Blend(palette.WindowBackground, "#000000", 0.16),
                Control: palette.ControlBackground,
                Popup: Blend(palette.ControlBackground, palette.PanelBackground, 0.35),
                SubtleBorder: Blend(palette.Border, palette.WindowBackground, 0.52),
                Hover: palette.HoverBackground,
                Selection: palette.ActiveBackground);
        }

        return new(
            Toolbar: palette.PanelBackground,
            Content: Blend(palette.ControlBackground, palette.WindowBackground, 0.12),
            Control: palette.ControlBackground,
            Popup: Blend(palette.ControlBackground, palette.PanelBackground, 0.35),
            SubtleBorder: Blend(palette.Border, palette.WindowBackground, 0.58),
            Hover: palette.HoverBackground,
            Selection: palette.ActiveBackground);
    }

    private static string Blend(string first, string second, double amount)
    {
        var a = Color.Parse(first);
        var b = Color.Parse(second);
        var ratio = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            Channel(a.A, b.A),
            Channel(a.R, b.R),
            Channel(a.G, b.G),
            Channel(a.B, b.B)).ToString();
        byte Channel(byte left, byte right) => (byte)Math.Round(left + (right - left) * ratio);
    }

    private sealed record AuxiliaryThemePalette(
        string Toolbar,
        string Content,
        string Control,
        string Popup,
        string SubtleBorder,
        string Hover,
        string Selection);
}

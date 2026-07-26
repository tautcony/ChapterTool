using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaThemeApplicationService : IThemeApplicationService
{
    public static IReadOnlyList<string> SourceGitColorKeys { get; } =
    [
        "Color.Window", "Color.WindowBorder", "Color.TitleBar", "Color.ToolBar", "Color.Popup",
        "Color.PopupBorder", "Color.Contents", "Color.Badge", "Color.BadgeFG", "Color.Conflict",
        "Color.Conflict.Foreground", "Color.Conflict.MineBG", "Color.Conflict.TheirsBG", "Color.Border0",
        "Color.Border1", "Color.Border2", "Color.FlatButton.Background", "Color.FlatButton.BackgroundHovered",
        "Color.FlatButton.FloatingBorder", "Color.FG1", "Color.FG2", "Color.Diff.EmptyBG",
        "Color.Diff.AddedBG", "Color.Diff.DeletedBG", "Color.Diff.AddedHighlight",
        "Color.Diff.DeletedHighlight", "Color.Diff.BlockBorderHighlight", "Color.Link", "Color.InlineCode",
        "Color.InlineCodeFG", "Color.HistoryBG"
    ];

    public const string WindowBackgroundBrushKey = "ChapterTool.WindowBackgroundBrush";
    public const string PanelBackgroundBrushKey = "ChapterTool.PanelBackgroundBrush";
    public const string ControlBackgroundBrushKey = "ChapterTool.ControlBackgroundBrush";
    public const string ControlForegroundBrushKey = "ChapterTool.ControlForegroundBrush";
    public const string MutedForegroundBrushKey = "ChapterTool.MutedForegroundBrush";
    public const string AccentBrushKey = "ChapterTool.AccentBrush";
    public const string AccentForegroundBrushKey = "ChapterTool.AccentForegroundBrush";
    public const string BorderBrushKey = "ChapterTool.BorderBrush";
    public const string HoverBackgroundBrushKey = "ChapterTool.HoverBackgroundBrush";
    public const string ActiveBackgroundBrushKey = "ChapterTool.ActiveBackgroundBrush";
    public const string FrameNeutralBrushKey = "ChapterTool.FrameNeutralBrush";
    public const string FrameAccurateBrushKey = "ChapterTool.FrameAccurateBrush";
    public const string FrameInexactBrushKey = "ChapterTool.FrameInexactBrush";
    public const string DiagnosticErrorBrushKey = "ChapterTool.DiagnosticErrorBrush";
    public const string AuxiliaryTitleBackgroundBrushKey = "ChapterTool.AuxiliaryTitleBackgroundBrush";
    public const string AuxiliaryToolbarBackgroundBrushKey = "ChapterTool.AuxiliaryToolbarBackgroundBrush";
    public const string AuxiliaryContentBackgroundBrushKey = "ChapterTool.AuxiliaryContentBackgroundBrush";
    public const string AuxiliaryControlBackgroundBrushKey = "ChapterTool.AuxiliaryControlBackgroundBrush";
    public const string AuxiliaryPopupBackgroundBrushKey = "ChapterTool.AuxiliaryPopupBackgroundBrush";
    public const string AuxiliaryBorderBrushKey = "ChapterTool.AuxiliaryBorderBrush";
    public const string AuxiliarySubtleBorderBrushKey = "ChapterTool.AuxiliarySubtleBorderBrush";
    public const string AuxiliaryHoverBackgroundBrushKey = "ChapterTool.AuxiliaryHoverBackgroundBrush";
    public const string AuxiliaryPressedBackgroundBrushKey = "ChapterTool.AuxiliaryPressedBackgroundBrush";
    public const string AuxiliarySelectionBackgroundBrushKey = "ChapterTool.AuxiliarySelectionBackgroundBrush";
    public const string AuxiliaryFocusBrushKey = "ChapterTool.AuxiliaryFocusBrush";
    public const string AuxiliaryDisabledForegroundBrushKey = "ChapterTool.AuxiliaryDisabledForegroundBrush";
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
        var auxiliary = AuxiliaryPalette(preset, palette);
        var resources = application.Resources;
        resources[WindowBackgroundBrushKey] = Brush(palette.WindowBackground);
        resources[PanelBackgroundBrushKey] = Brush(palette.PanelBackground);
        resources[ControlBackgroundBrushKey] = Brush(palette.ControlBackground);
        resources[ControlForegroundBrushKey] = Brush(palette.ControlForeground);
        resources[MutedForegroundBrushKey] = Brush(palette.MutedForeground);
        resources[AccentBrushKey] = Brush(palette.Accent);
        resources[AccentForegroundBrushKey] = Brush(palette.AccentForeground);
        resources[BorderBrushKey] = Brush(palette.Border);
        resources[HoverBackgroundBrushKey] = Brush(palette.HoverBackground);
        resources[ActiveBackgroundBrushKey] = Brush(palette.ActiveBackground);
        resources[FrameNeutralBrushKey] = Brush(palette.FrameNeutral);
        resources[FrameAccurateBrushKey] = Brush(palette.FrameAccurate);
        resources[FrameInexactBrushKey] = Brush(palette.FrameInexact);
        resources[DiagnosticErrorBrushKey] = Brush(palette.DiagnosticError);
        resources[AuxiliaryTitleBackgroundBrushKey] = Brush(auxiliary.Title);
        resources[AuxiliaryToolbarBackgroundBrushKey] = Brush(auxiliary.Toolbar);
        resources[AuxiliaryContentBackgroundBrushKey] = Brush(auxiliary.Content);
        resources[AuxiliaryControlBackgroundBrushKey] = Brush(auxiliary.Control);
        resources[AuxiliaryPopupBackgroundBrushKey] = Brush(auxiliary.Popup);
        resources[AuxiliaryBorderBrushKey] = Brush(auxiliary.Border);
        resources[AuxiliarySubtleBorderBrushKey] = Brush(auxiliary.SubtleBorder);
        resources[AuxiliaryHoverBackgroundBrushKey] = Brush(auxiliary.Hover);
        resources[AuxiliaryPressedBackgroundBrushKey] = Brush(auxiliary.Pressed);
        resources[AuxiliarySelectionBackgroundBrushKey] = Brush(auxiliary.Selection);
        resources[AuxiliaryFocusBrushKey] = Brush(palette.Accent);
        resources[AuxiliaryDisabledForegroundBrushKey] = Brush(palette.MutedForeground);
        resources[LogInformationBrushKey] = Brush(palette.Accent);
        resources[LogWarningBrushKey] = Brush(preset.BaseVariant == ThemeBaseVariant.Dark ? "#FFD580" : "#A15C00");
        resources[LogErrorBrushKey] = Brush(palette.DiagnosticError);
        ApplySourceGitColors(resources, preset, palette, auxiliary);
        application.RequestedThemeVariant = preset.BaseVariant == ThemeBaseVariant.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));

    private static void ApplySourceGitColors(
        IResourceDictionary resources,
        ThemePreset preset,
        ThemePalette palette,
        AuxiliaryThemePalette auxiliary)
    {
        var dark = preset.BaseVariant == ThemeBaseVariant.Dark;
        var colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Color.Window"] = palette.WindowBackground,
            ["Color.WindowBorder"] = palette.Border,
            ["Color.TitleBar"] = auxiliary.Title,
            ["Color.ToolBar"] = auxiliary.Toolbar,
            ["Color.Popup"] = auxiliary.Popup,
            ["Color.PopupBorder"] = dark ? auxiliary.SubtleBorder : palette.ControlBackground,
            ["Color.Contents"] = auxiliary.Content,
            ["Color.Badge"] = Blend(palette.Accent, palette.WindowBackground, dark ? 0.42 : 0.65),
            ["Color.BadgeFG"] = palette.ControlForeground,
            ["Color.Conflict"] = dark ? "#FAFAD2" : "#836C2E",
            ["Color.Conflict.Foreground"] = dark ? palette.WindowBackground : "#FFFFFF",
            ["Color.Conflict.MineBG"] = WithAlpha(palette.Accent, 0x40),
            ["Color.Conflict.TheirsBG"] = dark ? "#40FFB454" : "#40FF8C00",
            ["Color.Border0"] = auxiliary.SubtleBorder,
            ["Color.Border1"] = palette.Border,
            ["Color.Border2"] = auxiliary.SubtleBorder,
            ["Color.FlatButton.Background"] = auxiliary.Control,
            ["Color.FlatButton.BackgroundHovered"] = auxiliary.Hover,
            ["Color.FlatButton.FloatingBorder"] = palette.Border,
            ["Color.FG1"] = palette.ControlForeground,
            ["Color.FG2"] = palette.MutedForeground,
            ["Color.Diff.EmptyBG"] = dark ? "#3C000000" : "#10000000",
            ["Color.Diff.AddedBG"] = dark ? "#C03A5C3F" : "#80BFE6C1",
            ["Color.Diff.DeletedBG"] = dark ? "#C0633F3E" : "#80FF9797",
            ["Color.Diff.AddedHighlight"] = dark ? "#A0308D3C" : "#A7E1A7",
            ["Color.Diff.DeletedHighlight"] = dark ? "#A09F4247" : "#F19B9D",
            ["Color.Diff.BlockBorderHighlight"] = "#008B8B",
            ["Color.Link"] = palette.Accent,
            ["Color.InlineCode"] = auxiliary.Control,
            ["Color.InlineCodeFG"] = palette.ControlForeground,
            ["Color.HistoryBG"] = palette.PanelBackground
        };

        foreach (var (key, value) in colors)
        {
            resources[key] = Color.Parse(value);
        }
    }

    private static AuxiliaryThemePalette AuxiliaryPalette(ThemePreset preset, ThemePalette palette)
    {
        if (string.Equals(preset.Id, ThemePresetCatalog.DefaultPresetId, StringComparison.Ordinal))
        {
            return new(
                Title: "#CFDEEA",
                Toolbar: "#F0F5F9",
                Content: "#FAFAFA",
                Control: "#F8F8F8",
                Popup: "#F4F8FB",
                Border: "#898989",
                SubtleBorder: "#CFCFCF",
                Hover: "#FFFFFF",
                Pressed: "#DDEAF4",
                Selection: "#D7E7F3");
        }

        if (preset.BaseVariant == ThemeBaseVariant.Dark)
        {
            return new(
                Title: Blend(palette.WindowBackground, "#000000", 0.18),
                Toolbar: palette.PanelBackground,
                Content: Blend(palette.WindowBackground, "#000000", 0.16),
                Control: palette.ControlBackground,
                Popup: Blend(palette.ControlBackground, palette.PanelBackground, 0.35),
                Border: palette.Border,
                SubtleBorder: Blend(palette.Border, palette.WindowBackground, 0.52),
                Hover: palette.HoverBackground,
                Pressed: palette.ActiveBackground,
                Selection: palette.ActiveBackground);
        }

        return new(
            Title: Blend(palette.PanelBackground, palette.Accent, 0.12),
            Toolbar: palette.PanelBackground,
            Content: Blend(palette.ControlBackground, palette.WindowBackground, 0.12),
            Control: palette.ControlBackground,
            Popup: Blend(palette.ControlBackground, palette.PanelBackground, 0.35),
            Border: palette.Border,
            SubtleBorder: Blend(palette.Border, palette.WindowBackground, 0.58),
            Hover: palette.HoverBackground,
            Pressed: palette.ActiveBackground,
            Selection: palette.ActiveBackground);
    }

    private static string Blend(string first, string second, double amount)
    {
        var a = Color.Parse(first);
        var b = Color.Parse(second);
        var ratio = Math.Clamp(amount, 0, 1);
        byte Channel(byte left, byte right) => (byte)Math.Round(left + ((right - left) * ratio));
        return Color.FromArgb(
            Channel(a.A, b.A),
            Channel(a.R, b.R),
            Channel(a.G, b.G),
            Channel(a.B, b.B)).ToString();
    }

    private static string WithAlpha(string value, byte alpha)
    {
        var color = Color.Parse(value);
        return Color.FromArgb(alpha, color.R, color.G, color.B).ToString();
    }

    private sealed record AuxiliaryThemePalette(
        string Title,
        string Toolbar,
        string Content,
        string Control,
        string Popup,
        string Border,
        string SubtleBorder,
        string Hover,
        string Pressed,
        string Selection);
}

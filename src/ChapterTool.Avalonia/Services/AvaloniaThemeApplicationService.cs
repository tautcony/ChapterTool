using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaThemeApplicationService : IThemeApplicationService
{
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
        var dark = preset.BaseVariant == ThemeBaseVariant.Dark;
        resources[FrameNeutralBrushKey] = Brush(dark ? "#F3F4F5" : "#111111");
        resources[FrameAccurateBrushKey] = Brush(dark ? "#6EE7A0" : "#0F7A2F");
        resources[FrameInexactBrushKey] = Brush(dark ? "#FF8A80" : "#B42318");
        resources[DiagnosticErrorBrushKey] = Brush(dark ? "#FF8A80" : "#B42318");
        application.RequestedThemeVariant = preset.BaseVariant == ThemeBaseVariant.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));
}

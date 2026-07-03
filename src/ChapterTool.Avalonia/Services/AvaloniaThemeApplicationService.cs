using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaThemeApplicationService : IThemeApplicationService
{
    public const string BackChangeBrushKey = "ChapterTool.BackChangeBrush";
    public const string TextBackBrushKey = "ChapterTool.TextBackBrush";
    public const string MouseOverBrushKey = "ChapterTool.MouseOverBrush";
    public const string MouseDownBrushKey = "ChapterTool.MouseDownBrush";
    public const string BorderBrushKey = "ChapterTool.BorderBrush";
    public const string TextFrontBrushKey = "ChapterTool.TextFrontBrush";

    public void Apply(ThemeColorSettings settings)
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

        var resources = application.Resources;
        var defaults = ThemeColorSettings.Default;
        resources[BackChangeBrushKey] = Brush(settings.BackChange, defaults.BackChange);
        resources[TextBackBrushKey] = Brush(settings.TextBack, defaults.TextBack);
        resources[MouseOverBrushKey] = Brush(settings.MouseOverColor, defaults.MouseOverColor);
        resources[MouseDownBrushKey] = Brush(settings.MouseDownColor, defaults.MouseDownColor);
        resources[BorderBrushKey] = Brush(settings.BorderBackColor, defaults.BorderBackColor);
        resources[TextFrontBrushKey] = Brush(settings.TextFrontColor, defaults.TextFrontColor);
    }

    private static SolidColorBrush Brush(string value, string fallback) =>
        new(ParseColor(value, fallback));

    private static Color ParseColor(string value, string fallback)
    {
        try
        {
            return Color.Parse(value);
        }
        catch (FormatException)
        {
            return Color.Parse(fallback);
        }
    }
}

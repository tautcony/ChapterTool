using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ChapterTool.Avalonia.Headless.Tests.Headless;
using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Headless.Tests.Services;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AvaloniaThemeApplicationServiceTests
{
    [AvaloniaFact]
    public void ApplyWritesSemanticBrushResourcesAndDarkVariant()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaThemeApplicationService();

            try
            {
                service.Apply(new ThemeSettings("ayu-dark"));
                var palette = ThemePresetCatalog.Resolve("ayu-dark").Palette;

                Assert.Equal(Color.Parse(palette.WindowBackground), BrushColor(application, AvaloniaThemeApplicationService.WindowBackgroundBrushKey));
                Assert.Equal(Color.Parse(palette.PanelBackground), BrushColor(application, AvaloniaThemeApplicationService.PanelBackgroundBrushKey));
                Assert.Equal(Color.Parse(palette.ControlBackground), BrushColor(application, AvaloniaThemeApplicationService.ControlBackgroundBrushKey));
                Assert.Equal(Color.Parse(palette.ControlForeground), BrushColor(application, AvaloniaThemeApplicationService.ControlForegroundBrushKey));
                Assert.Equal(Color.Parse(palette.MutedForeground), BrushColor(application, AvaloniaThemeApplicationService.MutedForegroundBrushKey));
                Assert.Equal(Color.Parse(palette.Accent), BrushColor(application, AvaloniaThemeApplicationService.AccentBrushKey));
                Assert.Equal(Color.Parse(palette.AccentForeground), BrushColor(application, AvaloniaThemeApplicationService.AccentForegroundBrushKey));
                Assert.Equal(Color.Parse(palette.Border), BrushColor(application, AvaloniaThemeApplicationService.BorderBrushKey));
                Assert.Equal(Color.Parse(palette.HoverBackground), BrushColor(application, AvaloniaThemeApplicationService.HoverBackgroundBrushKey));
                Assert.Equal(Color.Parse(palette.ActiveBackground), BrushColor(application, AvaloniaThemeApplicationService.ActiveBackgroundBrushKey));
                Assert.Equal(ThemeVariant.Dark, application.RequestedThemeVariant);
            }
            finally
            {
                service.Apply(ThemeSettings.Default);
            }
        });
    }

    [AvaloniaFact]
    public void ApplyUnknownPresetFallsBackToDefaultLightVariant()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaThemeApplicationService();

            try
            {
                service.Apply(new ThemeSettings("missing"));

                Assert.Equal(
                    Color.Parse(ThemePresetCatalog.Default.Palette.WindowBackground),
                    BrushColor(application, AvaloniaThemeApplicationService.WindowBackgroundBrushKey));
                Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
            }
            finally
            {
                service.Apply(ThemeSettings.Default);
            }
        });
    }

    private static Color BrushColor(Application application, string key)
    {
        var brush = Assert.IsType<SolidColorBrush>(application.Resources[key]);
        return brush.Color;
    }
}

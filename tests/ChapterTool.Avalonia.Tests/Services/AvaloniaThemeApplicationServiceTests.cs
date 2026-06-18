using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Services;

public sealed class AvaloniaThemeApplicationServiceTests
{
    [AvaloniaFact]
    public void ApplyWritesThemeBrushResources()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaThemeApplicationService();

            try
            {
                service.Apply(new ThemeColorSettings(
                    "#010203",
                    "#111213",
                    "#212223",
                    "#313233",
                    "#414243",
                    "#515253"));

                Assert.Equal(Color.FromRgb(1, 2, 3), BrushColor(application, AvaloniaThemeApplicationService.BackChangeBrushKey));
                Assert.Equal(Color.FromRgb(17, 18, 19), BrushColor(application, AvaloniaThemeApplicationService.TextBackBrushKey));
                Assert.Equal(Color.FromRgb(33, 34, 35), BrushColor(application, AvaloniaThemeApplicationService.MouseOverBrushKey));
                Assert.Equal(Color.FromRgb(49, 50, 51), BrushColor(application, AvaloniaThemeApplicationService.MouseDownBrushKey));
                Assert.Equal(Color.FromRgb(65, 66, 67), BrushColor(application, AvaloniaThemeApplicationService.BorderBrushKey));
                Assert.Equal(Color.FromRgb(81, 82, 83), BrushColor(application, AvaloniaThemeApplicationService.TextFrontBrushKey));
            }
            finally
            {
                service.Apply(ThemeColorSettings.Default);
            }
        });
    }

    [AvaloniaFact]
    public void ApplyFallsBackToDefaultBrushForInvalidColor()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaThemeApplicationService();

            try
            {
                service.Apply(ThemeColorSettings.Default with { BackChange = "invalid" });

                Assert.Equal(Color.Parse(ThemeColorSettings.Default.BackChange), BrushColor(application, AvaloniaThemeApplicationService.BackChangeBrushKey));
            }
            finally
            {
                service.Apply(ThemeColorSettings.Default);
            }
        });
    }

    private static Color BrushColor(Application application, string key)
    {
        var brush = Assert.IsType<SolidColorBrush>(application.Resources[key]);
        return brush.Color;
    }
}

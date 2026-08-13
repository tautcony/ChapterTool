using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using ChapterTool.Avalonia.Headless.Tests.Headless;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.Headless.Tests.Services;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AvaloniaThemeApplicationServiceTests
{
    [AvaloniaTheory]
    [InlineData(ThemePresetCatalog.DefaultPresetId)]
    [InlineData("ayu-dark")]
    public void ApplyWritesImportedTokensFromCatalogAndBlendFormulas(string presetId)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia application was not initialized.");
        var service = new AvaloniaThemeApplicationService();

        try
        {
            service.Apply(new ThemeSettings(presetId));
            var preset = ThemePresetCatalog.Resolve(presetId);
            var expected = AvaloniaThemeApplicationService.ComputeImportedThemeColors(preset);

            Assert.All(
                AvaloniaThemeApplicationService.ImportedThemeColorKeys,
                key => Assert.Equal(Color.Parse(expected[key]), ColorResource(application, key)));
            Assert.Equal(Color.Parse(preset.Palette.FrameAccurate), BrushColor(application, AvaloniaThemeApplicationService.FrameAccurateBrushKey));
            Assert.Equal(Color.Parse(preset.Palette.DiagnosticError), BrushColor(application, AvaloniaThemeApplicationService.LogErrorBrushKey));
            AssertRuntimeResource(application, "Brush.Contents");
            AssertRuntimeResource(application, "Brush.Hover");
            AssertRuntimeResource(application, AvaloniaFontApplicationService.MonospaceFontFamilyKey);
            Assert.Equal(
                preset.BaseVariant == ThemeBaseVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light,
                application.RequestedThemeVariant);
        }
        finally
        {
            service.Apply(ThemeSettings.Default);
        }
    }

    [AvaloniaFact]
    public void ApplyUnknownPresetFallsBackToDefaultLightVariant()
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia application was not initialized.");
        var service = new AvaloniaThemeApplicationService();

        try
        {
            service.Apply(new ThemeSettings("missing"));
            var expected = AvaloniaThemeApplicationService.ComputeImportedThemeColors(ThemePresetCatalog.Default);

            Assert.Equal(Color.Parse(expected["Color.Window"]), ColorResource(application, "Color.Window"));
            Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
        }
        finally
        {
            service.Apply(ThemeSettings.Default);
        }
    }

    private static Color BrushColor(Application application, string key)
    {
        var brush = Assert.IsType<SolidColorBrush>(application.Resources[key]);
        return brush.Color;
    }

    private static Color ColorResource(Application application, string key) =>
        Assert.IsType<Color>(application.Resources[key]);

    private static void AssertRuntimeResource(Application application, string key)
    {
        Assert.True(application.TryGetResource(key, out var resource));
        Assert.NotNull(resource);
    }
}

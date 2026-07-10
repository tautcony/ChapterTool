using System.Globalization;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ThemePresetCatalogTests
{
    [Fact]
    public void Catalog_contains_complete_unique_presets()
    {
        var expectedIds = new[]
        {
            "avalonia-default", "solarized-light", "solarized-dark", "gruvbox-light",
            "gruvbox-dark", "ayu-light", "ayu-mirage", "ayu-dark"
        };

        Assert.Equal(expectedIds, ThemePresetCatalog.All.Select(static preset => preset.Id));
        Assert.Equal(ThemePresetCatalog.All.Count, ThemePresetCatalog.All.Select(static preset => preset.Id).Distinct().Count());
        Assert.Equal(ThemeBaseVariant.Dark, ThemePresetCatalog.Resolve("ayu-mirage").BaseVariant);
        Assert.All(ThemePresetCatalog.All, preset =>
        {
            Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", preset.Id);
            Assert.Equal(8, preset.Palette.PreviewSwatches.Count);
            Assert.All(preset.Palette.PreviewSwatches, color => Assert.Matches("^#[0-9A-F]{6}$", color));
            Assert.NotEqual(preset.Palette.ControlBackground, preset.Palette.HoverBackground);
            Assert.NotEqual(preset.Palette.ControlBackground, preset.Palette.ActiveBackground);
        });
    }

    [Fact]
    public void Catalog_semantic_pairs_meet_contrast_requirements()
    {
        Assert.All(ThemePresetCatalog.All, preset =>
        {
            var palette = preset.Palette;
            Assert.True(Contrast(palette.ControlForeground, palette.WindowBackground) >= 4.5, $"{preset.Name}: primary/window");
            Assert.True(Contrast(palette.ControlForeground, palette.ControlBackground) >= 4.5, $"{preset.Name}: primary/control");
            Assert.True(Contrast(palette.MutedForeground, palette.WindowBackground) >= 4.5, $"{preset.Name}: muted/window");
            Assert.True(Contrast(palette.AccentForeground, palette.Accent) >= 4.5, $"{preset.Name}: accent");
            Assert.True(Contrast(palette.Border, palette.ControlBackground) >= 3, $"{preset.Name}: border/control");
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public void Resolve_falls_back_to_default(string? presetId)
    {
        Assert.Equal(ThemePresetCatalog.DefaultPresetId, ThemePresetCatalog.Resolve(presetId).Id);
    }

    private static double Contrast(string first, string second)
    {
        var light = Math.Max(Luminance(first), Luminance(second));
        var dark = Math.Min(Luminance(first), Luminance(second));
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(string color)
    {
        var channels = Enumerable.Range(0, 3)
            .Select(index => int.Parse(color.AsSpan(1 + index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d)
            .Select(static value => value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4))
            .ToArray();
        return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
    }
}

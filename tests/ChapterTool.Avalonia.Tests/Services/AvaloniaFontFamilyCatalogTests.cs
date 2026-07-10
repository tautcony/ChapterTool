using System.Globalization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Services;

public sealed class AvaloniaFontFamilyCatalogTests
{
    [Fact]
    public void Catalog_filters_deduplicates_and_orders_canonical_names()
    {
        var catalog = new AvaloniaFontFamilyCatalog(
            [" zeta ", "Alpha", "alpha", "", null, "Beta"],
            CultureInfo.InvariantCulture);

        Assert.Equal(["Alpha", "Beta", "zeta"], catalog.Families.Select(static entry => entry.FamilyName));
        Assert.True(catalog.TryResolve(" alpha ", out var resolved));
        Assert.Equal("Alpha", resolved);
    }

    [Fact]
    public void Resolver_falls_back_each_category_independently()
    {
        var catalog = new AvaloniaFontFamilyCatalog(["Ui Family", "Mono Family"], CultureInfo.InvariantCulture);

        Assert.Equal(
            new FontSettings("Ui Family", ""),
            FontSettingsResolver.Resolve(new FontSettings("ui family", "Missing"), catalog));
        Assert.Equal(
            new FontSettings("", "Mono Family"),
            FontSettingsResolver.Resolve(new FontSettings("Missing", "mono family"), catalog));
    }

    [Fact]
    public void Catalog_entry_prefers_exact_then_same_language_then_canonical_name()
    {
        var entry = new FontFamilyCatalogEntry(
            "Canonical Family",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "简体名称",
                ["zh-TW"] = "繁體名稱",
                ["ja-JP"] = "日本語名"
            });

        Assert.Equal("繁體名稱", entry.GetDisplayName("zh-TW"));
        Assert.Equal("简体名称", entry.GetDisplayName("zh-SG"));
        Assert.Equal("日本語名", entry.GetDisplayName("ja-JP"));
        Assert.Equal("Canonical Family", entry.GetDisplayName("en-US"));
    }
}

using ChapterTool.Core.Exporting;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class XmlChapterLanguageCatalogTests
{
    [Theory]
    [InlineData("und")]
    [InlineData("zh")]
    [InlineData("ja")]
    [InlineData("en")]
    [InlineData("jpn")]
    [InlineData("fr")]
    [InlineData("FR")]
    [InlineData("En")]
    public void Catalog_accepts_common_and_iso_language_codes(string code)
    {
        Assert.True(XmlChapterLanguageCatalog.IsValidCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(null)]
    [InlineData("not-a-language")]
    public void Catalog_rejects_invalid_language_codes(string? code)
    {
        Assert.False(XmlChapterLanguageCatalog.IsValidCode(code));
    }
}

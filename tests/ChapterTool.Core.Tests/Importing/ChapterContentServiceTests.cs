using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Importing;

public sealed class ChapterContentServiceTests
{
    [Fact]
    public void ImportFormatsListOnlyPortableByteBasedFormats()
    {
        var formats = new ChapterContentService().ImportFormats;

        Assert.Contains(ChapterImportFormat.HdDvdXpl, formats);
        Assert.DoesNotContain(ChapterImportFormat.Media, formats);
        Assert.DoesNotContain(ChapterImportFormat.Bdmv, formats);
    }

    [Fact]
    public async Task ImportAsyncDetectsXmlWhenFileNameHasNoExtension()
    {
        var service = new ChapterContentService();
        var content = """
                      <?xml version="1.0"?>
                      <Chapters><EditionEntry><ChapterAtom><ChapterTimeStart>00:00:00.000</ChapterTimeStart></ChapterAtom></EditionEntry></Chapters>
                      """u8.ToArray();

        var result = await service.ImportAsync("chapters", content);

        Assert.True(result.Success);
        Assert.Equal(ChapterImportFormat.MatroskaXml, result.Groups.Single().Entries.Single().ChapterSet.ImportFormat);
        Assert.EndsWith(".xml", result.Groups.Single().SourcePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportedChapterSetCanBeExportedThroughSharedService()
    {
        var service = new ChapterContentService();
        var content = """
                      CHAPTER01=00:00:00.000
                      CHAPTER01NAME=Opening
                      CHAPTER02=00:01:00.000
                      CHAPTER02NAME=Middle
                      """u8.ToArray();
        var imported = await service.ImportAsync("chapters.txt", content);
        var chapterSet = imported.Groups.Single().Entries.Single().ChapterSet;

        var exported = service.Export(chapterSet, new ChapterExportOptions(ChapterExportFormat.Xml));

        Assert.True(exported.Success);
        Assert.Equal(".xml", exported.FileExtension);
        Assert.Contains("<Chapters>", exported.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsyncRoutesXplContentToTheCoreXplImporter()
    {
        var service = new ChapterContentService();
        var path = FixtureResolver.Fixture("Importing", "Disc", "Xpl", "VPLST001.XPL");

        var result = await service.ImportAsync(
            "VPLST001.XPL",
            await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(ChapterImportFormat.HdDvdXpl, result.Groups.Single().Entries.Single().ChapterSet.ImportFormat);
    }
}

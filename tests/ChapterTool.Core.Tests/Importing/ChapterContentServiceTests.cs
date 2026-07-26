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

    [Fact]
    public async Task ImportAsyncRoutesFlacBytesToEmbeddedCueImporter()
    {
        var service = new ChapterContentService();
        var cue = """
                  TITLE "Album"
                  FILE "audio.flac" WAVE
                    TRACK 01 AUDIO
                      TITLE "Track 1"
                      INDEX 01 00:00:00
                  """;
        var content = CreateFlacWithVorbisCue(cue);

        var result = await service.ImportAsync("music.flac", content, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(ChapterImportFormat.Cue, result.Groups.Single().Entries.Single().ChapterSet.ImportFormat);
        Assert.Equal("Track 1", result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single().Name);
    }

    [Fact]
    public async Task ImportAsyncRoutesTakBytesToEmbeddedCueImporter()
    {
        var service = new ChapterContentService();
        var cue = """
                  TITLE "Album"
                  FILE "audio.tak" WAVE
                    TRACK 01 AUDIO
                      TITLE "Track 1"
                      INDEX 01 00:00:00
                  """;
        var content = System.Text.Encoding.UTF8.GetBytes("tBaKpaddingCUESHEET=" + cue + "\0\0\0\0\0\0trailer");

        var result = await service.ImportAsync("music.tak", content, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(ChapterImportFormat.Cue, result.Groups.Single().Entries.Single().ChapterSet.ImportFormat);
        Assert.Equal("Track 1", result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single().Name);
    }

    [Theory]
    [InlineData(".flac")]
    [InlineData(".tak")]
    [InlineData(".mpls")]
    [InlineData(".ifo")]
    public void IsBinaryExtensionRecognizesPortableBinaryImports(string extension)
    {
        Assert.True(ChapterContentService.IsBinaryExtension(extension));
    }

    private static byte[] CreateFlacWithVorbisCue(string cue)
    {
        // Minimal FLAC: fLaC + one Vorbis comment block (type 4) containing cuesheet=...
        using var stream = new MemoryStream();
        stream.Write("fLaC"u8);
        var comment = System.Text.Encoding.UTF8.GetBytes("cuesheet=" + cue);
        var vendor = System.Text.Encoding.UTF8.GetBytes("ChapterTool");
        using var body = new MemoryStream();
        WriteLe32(body, vendor.Length);
        body.Write(vendor);
        WriteLe32(body, 1);
        WriteLe32(body, comment.Length);
        body.Write(comment);
        var payload = body.ToArray();

        // last block | type 4
        stream.WriteByte(0x84);
        stream.WriteByte((byte)((payload.Length >> 16) & 0xFF));
        stream.WriteByte((byte)((payload.Length >> 8) & 0xFF));
        stream.WriteByte((byte)(payload.Length & 0xFF));
        stream.Write(payload);
        return stream.ToArray();
    }

    private static void WriteLe32(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 24) & 0xFF));
    }
}

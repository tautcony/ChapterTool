using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class TextImporterTests
{
    private readonly ChapterTimeFormatter formatter = new();

    [Fact]
    public async Task OgmImporterReadsExistingSampleLeniently()
    {
        var importer = new OgmChapterImporter(formatter);
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "Ogm", "00001.txt")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PartialParse");
        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal(6, chapters.Count);
        Assert.Equal(TimeSpan.Zero, chapters[0].Time);
        Assert.Equal("Chapter 06", chapters[5].Name);
    }

    [Fact]
    public async Task OgmImporterNormalizesFirstTimestamp()
    {
        var importer = new OgmChapterImporter(formatter);
        var result = importer.ImportText(
            """
            CHAPTER01=00:01:00.000
            CHAPTER01NAME=Intro
            CHAPTER02=00:01:30.000
            CHAPTER02NAME=Middle
            """);

        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal(TimeSpan.Zero, chapters[0].Time);
        Assert.Equal(TimeSpan.FromSeconds(30), chapters[1].Time);
    }

    [Fact]
    public async Task OgmImporterFailsInvalidFirstLine()
    {
        var importer = new OgmChapterImporter(formatter);
        var result = importer.ImportText("CHAPTER01NAME=Intro");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OgmInvalidFirstLine");
    }

    [Fact]
    public async Task OgmImporterReturnsPartialAfterMalformedLaterContent()
    {
        var importer = new OgmChapterImporter(formatter);
        var result = importer.ImportText(
            """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Intro
            unexpected
            """);

        Assert.True(result.Success);
        Assert.True(result.IsPartial);
        Assert.Single(result.Groups.Single().Options.Single().ChapterInfo.Chapters);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void OgmImporterReturnsPartialAfterDanglingTime()
    {
        var importer = new OgmChapterImporter(formatter);
        var result = importer.ImportText(
            """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Intro
            CHAPTER02=00:00:10.000
            """);

        Assert.True(result.Success);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PartialParse");
        Assert.Single(result.Groups.Single().Options.Single().ChapterInfo.Chapters);
    }

    [Fact]
    public async Task WebVttImporterReadsExistingSample()
    {
        var importer = new WebVttChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "WebVtt", "chapter.vtt")),
            CancellationToken.None);

        Assert.True(result.Success);
        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal(7, chapters.Count);
        Assert.Equal("Introduction", chapters[0].Name);
        Assert.Equal(TimeSpan.FromMilliseconds(28206), chapters[1].Time);
    }

    [Fact]
    public async Task WebVttImporterSkipsCueIds()
    {
        var importer = new WebVttChapterImporter();
        var result = importer.ImportText(
            """
            WEBVTT

            cue-1
            00:00:03.000 --> 00:00:05.000
            Intro
            """);

        Assert.True(result.Success);
        Assert.Equal("Intro", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Theory]
    [InlineData("BAD\n\n00:00:00.000 --> 00:00:01.000\nIntro", "WebVttInvalidHeader")]
    [InlineData("WEBVTT\n\nbad timing\nIntro", "WebVttMalformedCue")]
    [InlineData("WEBVTT\n\n00:00:00.000 --> 00:00:01.000 align:start\nIntro", "WebVttUnsupportedTimingSettings")]
    public async Task WebVttImporterFailsMalformedInput(string text, string code)
    {
        var importer = new WebVttChapterImporter();
        var result = importer.ImportText(text);

        Assert.False(result.Success);
        Assert.Empty(result.Groups);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task XmlImporterCreatesOneOptionPerEdition()
    {
        var importer = new XmlChapterImporter(formatter);
        var result = importer.ImportText(
            """
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Edition 1</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Edition 2</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """);

        Assert.True(result.Success);
        Assert.Equal(2, result.Groups.Single().Options.Count);
        Assert.Equal("Edition 1", result.Groups.Single().Options[0].ChapterInfo.Chapters.Single().Name);
        Assert.Equal("Edition 2", result.Groups.Single().Options[1].ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task XmlImporterFlattensNestedAtomsAndReadsEndBoundary()
    {
        var importer = new XmlChapterImporter(formatter);
        var result = importer.ImportText(
            """
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterTimeEnd>00:00:30.000000000</ChapterTimeEnd>
                  <ChapterDisplay><ChapterString>Parent</ChapterString></ChapterDisplay>
                  <ChapterAtom>
                    <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
                    <ChapterDisplay><ChapterString>Child</ChapterString></ChapterDisplay>
                  </ChapterAtom>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """);

        Assert.True(result.Success);
        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal(["Parent", "Child", "Parent"], chapters.Select(chapter => chapter.Name));
        Assert.Equal(TimeSpan.FromSeconds(30), chapters[0].End);
        Assert.Equal(TimeSpan.FromSeconds(30), chapters[2].Time);
    }

    [Fact]
    public async Task XmlImporterRemovesAdjacentDuplicateTimes()
    {
        var importer = new XmlChapterImporter(formatter);
        var result = importer.ImportText(
            """
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Duplicate</ChapterString></ChapterDisplay>
                </ChapterAtom>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Kept</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """);

        var chapter = result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single();
        Assert.Equal("Kept", chapter.Name);
        Assert.Equal(1, chapter.Number);
    }

    [Theory]
    [InlineData("<NotChapters />", "XmlInvalidRoot")]
    [InlineData("<Chapters><EditionEntry><ChapterAtom><ChapterDisplay><ChapterString>No Time</ChapterString></ChapterDisplay></ChapterAtom></EditionEntry></Chapters>", "XmlNoChapters")]
    public async Task XmlImporterFailsInvalidDocuments(string xml, string code)
    {
        var importer = new XmlChapterImporter(formatter);
        var result = importer.ImportText(xml);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }
}

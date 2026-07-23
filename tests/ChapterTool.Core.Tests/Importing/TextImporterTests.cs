using System.Diagnostics.CodeAnalysis;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class TextImporterTests
{
    private readonly ChapterTimeFormatter formatter = new();

    [Fact]
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1027:Use tabs correctly", Justification = "Test methods require for tab")]
    public Task OgmImporterReadsExistingSampleLeniently()
    {
        try
        {
            var importer = new OgmChapterImporter(formatter);
            const string ogmText = """




                                   CHAPTER01 = 00:00:00.000
                                   CHAPTER01NAME=	Chapter 01

                                   CHAPTER02=00:00:41.041
                                   CHAPTER02NAME=Chapter 02
                                   CHAPTER03=00:02:12.799
                                   CHAPTER03NAME=Chapter 03
                                   CHAPTER04=00:03:36.258

                                   CHAPTER04NAME=Chapter 04
                                   CHAPTER05=00:04:37.944
                                   CHAPTER05NAME=Chapter 05
                                   CHAPTER06=00:05:44.928
                                   CHAPTER06NAME=Chapter 06

                                   CHAPTER07=00:08:59.247
                                   """;
            var result = importer.ImportText(ogmText);

            Assert.True(result.Success);
            Assert.True(result.IsPartial);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.PartialParse);
            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(6, chapters.Count);
            Assert.Equal(TimeSpan.Zero, chapters[0].StartTime);
            Assert.Equal("Chapter 06", chapters[5].Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task OgmImporterNormalizesFirstTimestamp()
    {
        try
        {
            var importer = new OgmChapterImporter(formatter);
            var result = importer.ImportText(
                """
                CHAPTER01=00:01:00.000
                CHAPTER01NAME=Intro
                CHAPTER02=00:01:30.000
                CHAPTER02NAME=Middle
                """);

            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(TimeSpan.Zero, chapters[0].StartTime);
            Assert.Equal(TimeSpan.FromSeconds(30), chapters[1].StartTime);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task OgmImporterFailsInvalidFirstLine()
    {
        try
        {
            var importer = new OgmChapterImporter(formatter);
            var result = importer.ImportText("CHAPTER01NAME=Intro");

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.OgmInvalidFirstLine);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task OgmImporterReturnsPartialAfterMalformedLaterContent()
    {
        try
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
            Assert.Single(result.Groups.Single().Entries.Single().ChapterSet.Chapters);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public void TextImporterFallsBackToOgmForNonPremiereTxt()
    {
        var importer = new TextChapterImporter(formatter);
        var result = importer.ImportText(
            """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Intro
            CHAPTER02=00:00:10.000
            CHAPTER02NAME=Main
            """);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.Ogm, info.ImportFormat);
        Assert.Equal(["Intro", "Main"], info.Chapters.Select(static chapter => chapter.Name));
    }

    [Fact]
    public void PremiereImporterReadsTabSeparatedMarkerListAndIgnoresNonChapterRows()
    {
        var importer = new PremiereMarkerListImporter(formatter);
        const string content =
            "Marker Name\tDescription\tIn\tOut\tDuration\tMarker Type\tComment\r\n" +
            "Intro\t\t00:00:00.000\t00:00:10.000\t00:00:10.000\tChapter\tOpening\r\n" +
            "Note\t\t00:00:05.000\t00:00:06.000\t00:00:01.000\tComment\tIgnore me\r\n" +
            "Part A\t\t00:01:23.456\t00:01:30.000\t00:00:06.544\tChapter\t\r\n";

        var result = importer.ImportText(content, "markers.txt");

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.PremiereMarkers, info.ImportFormat);
        Assert.Equal("markers.txt", info.SourceName);
        Assert.Equal(2, info.Chapters.Count);
        Assert.Equal("Intro", info.Chapters[0].Name);
        Assert.Equal(TimeSpan.Zero, info.Chapters[0].StartTime);
        Assert.Equal("Part A", info.Chapters[1].Name);
        Assert.Equal(TimeSpan.FromMilliseconds(83456), info.Chapters[1].StartTime);
    }

    [Fact]
    public void PremiereImporterUsesCommentAsFallbackNameForCsv()
    {
        var importer = new PremiereMarkerListImporter(formatter);
        const string content =
            "\"Marker Name\",\"In\",\"Marker Type\",\"Comment\"\r\n" +
            "\"\",\"00:00:12.345\",\"Chapter\",\"Scene 02\"\r\n";

        var result = importer.ImportText(content);

        Assert.True(result.Success, Diagnostics(result));
        var chapter = result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single();
        Assert.Equal("Scene 02", chapter.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(12345), chapter.StartTime);
    }

    [Fact]
    public void PremiereImporterHandlesQuotedCommaAndFrameTime()
    {
        var importer = new PremiereMarkerListImporter(formatter);
        const string content =
            "\"Marker Name\",\"In\",\"Marker Type\"\r\n" +
            "\"Act \"\"One, Start\"\"\",\"00:00:10:12\",\"Chapter\"\r\n";

        var result = importer.ImportText(content);

        Assert.True(result.Success, Diagnostics(result));
        var chapter = result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single();
        Assert.Equal("Act \"One, Start\"", chapter.Name);
        Assert.Equal(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks((long)Math.Round(12 * TimeSpan.TicksPerSecond / 23.976M)), chapter.StartTime);
    }

    [Fact]
    public void TextImporterDetectsPremiereTxtBeforeOgmFallback()
    {
        var importer = new TextChapterImporter(formatter);
        const string content =
            "Marker Name\tIn\tMarker Type\r\n" +
            "Intro\t00:00:00.000\tChapter\r\n";

        var result = importer.ImportText(content, "markers.txt");

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.PremiereMarkers, info.ImportFormat);
        Assert.Equal("Intro", info.Chapters.Single().Name);
    }

    [Theory]
    [InlineData("Marker Name,Comment\r\nIntro,Missing time", ChapterDiagnosticSource.PremiereMarkerList, ChapterDiagnosticReason.Invalid)]
    [InlineData("Marker Name,In,Marker Type\r\nNote,not-time,Chapter", ChapterDiagnosticSource.PremiereMarkerList, ChapterDiagnosticReason.Invalid)]
    public void PremiereImporterFailsInvalidMarkerLists(string text, ChapterDiagnosticSource source, ChapterDiagnosticReason reason)
    {
        var importer = new PremiereMarkerListImporter(formatter);
        var result = importer.ImportText(text);

        Assert.False(result.Success);
        Assert.Empty(result.Groups);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == new ChapterDiagnosticCode(source, reason));
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
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.PartialParse);
        Assert.Single(result.Groups.Single().Entries.Single().ChapterSet.Chapters);
    }

    [Fact]
    public Task WebVttImporterReadsExistingSample()
    {
        try
        {
            var importer = new WebVttChapterImporter();
            const string vttText = """
                                   WEBVTT

                                   chapter-1
                                   00:00:00.000 --> 00:00:26.000
                                   Introduction

                                   chapter-2
                                   00:00:28.206 --> 00:01:02.000
                                   Watch out!

                                   chapter-3
                                   00:01:02.034 --> 00:03:10.000
                                   Let's go

                                   chapter-4
                                   00:03:10.014 --> 00:05:40.000
                                   The machine

                                   chapter-5
                                   00:05:41.208 --> 00:07:26.000
                                   Close your eyes

                                   chapter-6
                                   00:07:27.125 --> 00:08:12.000
                                   There's nothing there

                                   chapter-7
                                   00:08:13.000 --> 00:09:07.500
                                   The Colossus of Rhodes
                                   """;
            var result = WebVttChapterImporter.ImportText(vttText);

            Assert.True(result.Success);
            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(7, chapters.Count);
            Assert.Equal("Introduction", chapters[0].Name);
            Assert.Equal(TimeSpan.FromMilliseconds(28206), chapters[1].StartTime);
            Assert.Equal(TimeSpan.FromSeconds(26), chapters[0].EndTime);
            Assert.Equal(TimeSpan.FromMilliseconds(547500), chapters[^1].EndTime);
            Assert.Equal(TimeSpan.FromMilliseconds(547500), result.Groups.Single().Entries.Single().ChapterSet.Duration);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task WebVttImporterSkipsCueIds()
    {
        try
        {
            var result = WebVttChapterImporter.ImportText(
                """
                WEBVTT

                cue-1
                00:00:03.000 --> 00:00:05.000
                Intro
                """);

            Assert.True(result.Success);
            Assert.Equal("Intro", result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single().Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Theory]
    [InlineData("BAD\n\n00:00:00.000 --> 00:00:01.000\nIntro", ChapterDiagnosticSource.WebVttHeader, ChapterDiagnosticReason.Invalid)]
    [InlineData("WEBVTT\n\nbad timing\nIntro", ChapterDiagnosticSource.WebVttCue, ChapterDiagnosticReason.Malformed)]
    [InlineData("WEBVTT\n\n00:00:00.000 --> 00:00:01.000 align:start\nIntro", ChapterDiagnosticSource.WebVttTimingSettings, ChapterDiagnosticReason.Unsupported)]
    public Task WebVttImporterFailsMalformedInput(string text, ChapterDiagnosticSource source, ChapterDiagnosticReason reason)
    {
        try
        {
            var importer = new WebVttChapterImporter();
            var result = WebVttChapterImporter.ImportText(text);

            Assert.False(result.Success);
            Assert.Empty(result.Groups);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == new ChapterDiagnosticCode(source, reason));
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task XmlImporterCreatesOneOptionPerEdition()
    {
        try
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
            Assert.Equal(2, result.Groups.Single().Entries.Count);
            Assert.Equal("Edition 1", result.Groups.Single().Entries[0].ChapterSet.Chapters.Single().Name);
            Assert.Equal("Edition 2", result.Groups.Single().Entries[1].ChapterSet.Chapters.Single().Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task XmlImporterFlattensNestedAtomsAndPreservesEndMetadata()
    {
        try
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
            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(["Parent", "Child"], chapters.Select(chapter => chapter.Name));
            Assert.Equal(TimeSpan.FromSeconds(30), chapters[0].EndTime);
            Assert.Null(chapters[1].EndTime);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task XmlImporterRemovesAdjacentDuplicateTimes()
    {
        try
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

            var chapter = result.Groups.Single().Entries.Single().ChapterSet.Chapters.Single();
            Assert.Equal("Kept", chapter.Name);
            Assert.Equal(1, chapter.DisplayNumber);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Theory]
    [InlineData("<NotChapters />", ChapterDiagnosticSource.XmlRoot, ChapterDiagnosticReason.Invalid)]
    [InlineData("<Chapters><EditionEntry><ChapterAtom><ChapterDisplay><ChapterString>No Time</ChapterString></ChapterDisplay></ChapterAtom></EditionEntry></Chapters>", ChapterDiagnosticSource.XmlChapters, ChapterDiagnosticReason.None)]
    public Task XmlImporterFailsInvalidDocuments(string xml, ChapterDiagnosticSource source, ChapterDiagnosticReason reason)
    {
        try
        {
            var importer = new XmlChapterImporter(formatter);
            var result = importer.ImportText(xml);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == new ChapterDiagnosticCode(source, reason));
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task XmlImporterRejectsDtdInputFromTextStreamAndPath()
    {
        const string xml = """
                           <!DOCTYPE Chapters [<!ENTITY injected "untrusted">]>
                           <Chapters>
                             <EditionEntry>
                               <ChapterAtom>
                                 <ChapterTimeStart>00:00:00.000</ChapterTimeStart>
                                 <ChapterDisplay><ChapterString>&injected;</ChapterString></ChapterDisplay>
                               </ChapterAtom>
                             </EditionEntry>
                           </Chapters>
                           """;
        var importer = new XmlChapterImporter(formatter);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var path = Path.Combine(Path.GetTempPath(), $"chaptertool-{Guid.NewGuid():N}.xml");

        try
        {
            await File.WriteAllTextAsync(path, xml, TestContext.Current.CancellationToken);
            var textResult = importer.ImportText(xml);
            var streamResult = await importer.ImportAsync(new ChapterImportRequest("untrusted.xml", stream), TestContext.Current.CancellationToken);
            var pathResult = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.All([textResult, streamResult, pathResult], result =>
            {
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidXml);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task XmlLiveSamplePreservesUtf8ChapterNames()
    {
        var importer = new XmlChapterImporter(formatter);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "Xml", "[philosophy-raws][Hatsune Miku Magical Mirai 2014 in OSAKA][Live].xml")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
        Assert.Equal(30, chapters.Count);
        Assert.Equal("01 High-energy Particle", chapters[0].Name);
        Assert.Contains(chapters, chapter => chapter.Name.Contains("カゲロウデイズ", StringComparison.Ordinal));
        Assert.Equal(TimeSpan.FromMilliseconds(6789383), chapters[^1].StartTime);
    }

    [Fact]
    public async Task XmlChapter25SampleMatchesDeduplicatedLegacyOutput()
    {
        var importer = new XmlChapterImporter(formatter);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "Xml", "XML_Chapter_25.xml")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
        Assert.Equal(17, chapters.Count);
        Assert.Equal("Chapter 01", chapters[0].Name.Trim());
        Assert.Equal(TimeSpan.FromMilliseconds(267080), chapters[1].StartTime);
    }

    [Fact]
    public Task XmlHiddenChapterSampleImportsBothEditionsWithEndTimes()
    {
        try
        {
            var importer = new XmlChapterImporter(formatter);
            const string xmlText = """
                                   <?xml version="1.0" encoding="ISO-8859-1"?>

                                   <Chapters>
                                     <EditionEntry>
                                       <ChapterAtom>
                                         <ChapterTimeStart>00:00:00.000</ChapterTimeStart>
                                         <ChapterDisplay>
                                           <ChapterString>Intro</ChapterString>
                                           <ChapterLanguage>eng</ChapterLanguage>
                                         </ChapterDisplay>
                                       </ChapterAtom>
                                       <ChapterAtom>
                                         <ChapterTimeStart>00:01:00.000</ChapterTimeStart>
                                         <ChapterDisplay>
                                           <ChapterString>Act 1</ChapterString>
                                           <ChapterLanguage>eng</ChapterLanguage>
                                         </ChapterDisplay>
                                       </ChapterAtom>
                                       <ChapterAtom>
                                         <ChapterTimeStart>00:05:30.000</ChapterTimeStart>
                                         <ChapterDisplay>
                                           <ChapterString>Act 2</ChapterString>
                                           <ChapterLanguage>eng</ChapterLanguage>
                                         </ChapterDisplay>
                                       </ChapterAtom>
                                       <ChapterAtom>
                                         <ChapterTimeStart>00:12:20.000</ChapterTimeStart>
                                         <ChapterTimeEnd>00:12:55.000</ChapterTimeEnd>
                                         <ChapterDisplay>
                                           <ChapterString>Credits</ChapterString>
                                           <ChapterLanguage>eng</ChapterLanguage>
                                         </ChapterDisplay>
                                       </ChapterAtom>
                                     </EditionEntry>

                                     <EditionEntry>
                                       <ChapterAtom>
                                         <ChapterTimeStart>00:02:00.000</ChapterTimeStart>
                                         <ChapterTimeEnd>00:04:00.000</ChapterTimeEnd>
                                         <ChapterDisplay>
                                           <ChapterString>A hidden and not enabled chapter.</ChapterString>
                                           <ChapterLanguage>eng</ChapterLanguage>
                                         </ChapterDisplay>
                                         <ChapterFlagHidden>1</ChapterFlagHidden>
                                         <ChapterFlagEnabled>0</ChapterFlagEnabled>
                                       </ChapterAtom>
                                     </EditionEntry>
                                   </Chapters>
                                   """;

            var result = importer.ImportText(xmlText);

            Assert.True(result.Success, Diagnostics(result));
            var entries = result.Groups.Single().Entries;
            Assert.Equal(2, entries.Count);
            Assert.Equal(["Intro", "Act 1", "Act 2", "Credits"], entries[0].ChapterSet.Chapters.Select(static chapter => chapter.Name));
            Assert.Equal(TimeSpan.FromMinutes(2), entries[1].ChapterSet.Chapters.Single().StartTime);
            Assert.Equal(TimeSpan.FromMinutes(4), entries[1].ChapterSet.Chapters.Single().EndTime);
            Assert.Equal("A hidden and not enabled chapter.", entries[1].ChapterSet.Chapters.Single().Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task XmlImporterFlattensDtdFreeNestedChapters()
    {
        try
        {
            var importer = new XmlChapterImporter(formatter);

            var result = importer.ImportText(
                """
                <Chapters>
                  <EditionEntry>
                    <ChapterAtom>
                      <ChapterTimeStart>00:00:00.000</ChapterTimeStart>
                      <ChapterTimeEnd>00:06:24.000</ChapterTimeEnd>
                      <ChapterDisplay><ChapterString>Overture</ChapterString></ChapterDisplay>
                      <ChapterAtom>
                        <ChapterTimeStart>00:06:24.000</ChapterTimeStart>
                        <ChapterDisplay><ChapterString>Dialog</ChapterString></ChapterDisplay>
                      </ChapterAtom>
                    </ChapterAtom>
                  </EditionEntry>
                </Chapters>
                """);

            Assert.True(result.Success, Diagnostics(result));
            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(2, chapters.Count);
            Assert.Equal("Overture", chapters[0].Name);
            Assert.Equal(TimeSpan.Zero, chapters[0].StartTime);
            Assert.Equal(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(24)), chapters[0].EndTime);
            Assert.Equal("Dialog", chapters[^1].Name);
            Assert.Equal(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(24)), chapters[^1].StartTime);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task XmlImporterPreservesIso88591EncodingFromDeclarationWithoutDtd()
    {
        var importer = new XmlChapterImporter(formatter);
        const string xml = """
                           <?xml version="1.0" encoding="ISO-8859-1"?>
                           <Chapters>
                             <EditionEntry>
                               <ChapterAtom>
                                 <ChapterTimeStart>00:00:00.000</ChapterTimeStart>
                                 <ChapterDisplay><ChapterString>Schätzchen</ChapterString></ChapterDisplay>
                               </ChapterAtom>
                             </EditionEntry>
                           </Chapters>
                           """;
        using var stream = new MemoryStream(System.Text.Encoding.Latin1.GetBytes(xml));

        var result = await importer.ImportAsync(
            new ChapterImportRequest("latin1.xml", stream),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
        Assert.Contains(chapters, c => c.Name.Contains("Schätzchen", StringComparison.Ordinal));
    }

    [Fact]
    public Task XmlImporterSetsDefaultEntryIndexFromEditionFlagDefault()
    {
        try
        {
            var importer = new XmlChapterImporter(formatter);
            const string xml = """
                               <?xml version="1.0" encoding="UTF-8"?>
                               <Chapters>
                                 <EditionEntry>
                                   <EditionFlagDefault>0</EditionFlagDefault>
                                   <ChapterAtom>
                                     <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                                     <ChapterDisplay><ChapterString>First Edition</ChapterString></ChapterDisplay>
                                   </ChapterAtom>
                                 </EditionEntry>
                                 <EditionEntry>
                                   <EditionFlagDefault>1</EditionFlagDefault>
                                   <ChapterAtom>
                                     <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
                                     <ChapterDisplay><ChapterString>Default Edition</ChapterString></ChapterDisplay>
                                   </ChapterAtom>
                                 </EditionEntry>
                               </Chapters>
                               """;

            var result = importer.ImportText(xml);

            Assert.True(result.Success, Diagnostics(result));
            var group = result.Groups.Single();
            Assert.Equal(2, group.Entries.Count);
            Assert.Equal(1, group.DefaultEntryIndex);
            Assert.Equal("Default Edition", group.Entries[1].ChapterSet.Chapters.Single().Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public Task XmlImporterKeepsFirstDefaultEditionWhenMultipleEditionsAreDefault()
    {
        try
        {
            var importer = new XmlChapterImporter(formatter);
            const string xml = """
                               <?xml version="1.0" encoding="UTF-8"?>
                               <Chapters>
                                 <EditionEntry>
                                   <EditionFlagDefault>1</EditionFlagDefault>
                                   <ChapterAtom>
                                     <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                                     <ChapterDisplay><ChapterString>First Default</ChapterString></ChapterDisplay>
                                   </ChapterAtom>
                                 </EditionEntry>
                                 <EditionEntry>
                                   <EditionFlagDefault>1</EditionFlagDefault>
                                   <ChapterAtom>
                                     <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
                                     <ChapterDisplay><ChapterString>Second Default</ChapterString></ChapterDisplay>
                                   </ChapterAtom>
                                 </EditionEntry>
                               </Chapters>
                               """;

            var result = importer.ImportText(xml);

            Assert.True(result.Success, Diagnostics(result));
            var group = result.Groups.Single();
            Assert.Equal(0, group.DefaultEntryIndex);
            Assert.Equal("First Default", group.Entries[group.DefaultEntryIndex].ChapterSet.Chapters.Single().Name);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task XmlFourEditionLegacySampleCreatesOneOptionPerEdition()
    {
        var importer = new XmlChapterImporter(formatter);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "Xml", "xml (T2 - 4 Editions).xml")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = result.Groups.Single().Entries;
        Assert.Equal(4, entries.Count);
        Assert.All(entries, entry => Assert.NotEmpty(entry.ChapterSet.Chapters));
        Assert.Equal("Prologue", entries[0].ChapterSet.Chapters[0].Name);
    }

    [Theory]
    [MemberData(nameof(XmlSampleExpectations))]
    public async Task AdditionalXmlSamplesMatchExpectedEditionShape(XmlSampleExpectation sample)
    {
        var importer = new XmlChapterImporter(formatter);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Text", "Xml", sample.FileName)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = result.Groups.Single().Entries;
        Assert.Equal(sample.Entries.Length, entries.Count);
        for (var i = 0; i < sample.Entries.Length; i++)
        {
            var expected = sample.Entries[i];
            var chapters = entries[i].ChapterSet.Chapters;
            Assert.Equal(expected.ChapterCount, chapters.Count);
            Assert.Equal(expected.Duration, entries[i].ChapterSet.Duration);
            Assert.Equal(expected.FirstName, chapters[0].Name);
            Assert.Equal(expected.FirstTime, chapters[0].StartTime);
            Assert.Equal(expected.LastName, chapters[^1].Name);
            Assert.Equal(expected.LastTime, chapters[^1].StartTime);
        }
    }

    public static TheoryData<XmlSampleExpectation> XmlSampleExpectations() =>
    [
        XmlSample("[philosophy-raws][Hatsune Miku Magical Mirai 2014 in OSAKA][Live].xml",
        [
            new(30, Ms(6789383), "01 High-energy Particle", TimeSpan.Zero, "End Roll", Ms(6789383))
        ]),

        XmlSample("Angel Beats! - NCOP_Ordered_Chapter.xml", AngelBeatsEditions())
    ];

    public sealed record XmlSampleExpectation(string FileName, XmlOptionExpectation[] Entries);

    public sealed record XmlOptionExpectation(
        int ChapterCount,
        TimeSpan Duration,
        string FirstName,
        TimeSpan FirstTime,
        string LastName,
        TimeSpan LastTime);

    private static XmlOptionExpectation[] AngelBeatsEditions() =>
        Enumerable.Range(0, 14)
            .Select(static _ => new XmlOptionExpectation(3, Ms(59601), "Part A", TimeSpan.Zero, "Part C", Ms(59601)))
            .ToArray();

    private static XmlSampleExpectation XmlSample(string fileName, XmlOptionExpectation[] entries) =>
        new(fileName, entries);

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}

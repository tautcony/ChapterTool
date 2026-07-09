using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class XplImporterTests
{
    [Theory]
    [MemberData(nameof(SampleExpectations))]
    public async Task SampleXplFilesMatchExpectedTitleShape(SampleExpectation sample)
    {
        var importer = new XplChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Xpl", sample.FileName)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = result.Groups.Single().Entries;
        Assert.Equal(sample.Entries.Length, entries.Count);
        for (var i = 0; i < sample.Entries.Length; i++)
        {
            var expected = sample.Entries[i];
            var actual = entries[i].ChapterSet;
            Assert.Equal(ChapterImportFormat.HdDvdXpl, actual.ImportFormat);
            Assert.Equal(expected.Title, actual.Title);
            Assert.Equal(expected.SourceName, actual.SourceName);
            Assert.Equal(24, actual.FramesPerSecond);
            AssertTimeNear(expected.Duration, actual.Duration);
            Assert.Equal(expected.ChapterCount, actual.Chapters.Count);
            AssertTimeNear(expected.FirstTime, actual.Chapters[0].StartTime);
            Assert.Equal(expected.FirstName, actual.Chapters[0].Name);
            AssertTimeNear(expected.LastTime, actual.Chapters[^1].StartTime);
            Assert.Equal(expected.LastName, actual.Chapters[^1].Name);
        }
    }

    [Fact]
    public async Task Vplst000MatchesLegacyExpectedTimes()
    {
        var xpl = new XplChapterImporter();

        var xplResult = await xpl.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Xpl", "VPLST000.XPL")),
            TestContext.Current.CancellationToken);

        Assert.True(xplResult.Success, Diagnostics(xplResult));
        var expectedTimes = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(282),
            TimeSpan.FromSeconds(557),
            TimeSpan.FromSeconds(775),
            TimeSpan.FromSeconds(996),
            TimeSpan.FromSeconds(1199),
            TimeSpan.FromSeconds(1302),
            TimeSpan.FromSeconds(1473),
            TimeSpan.FromSeconds(1705),
            TimeSpan.FromSeconds(1939),
            TimeSpan.FromSeconds(2263),
            TimeSpan.FromSeconds(2456),
            TimeSpan.FromSeconds(2585),
            TimeSpan.FromSeconds(2855),
            TimeSpan.FromSeconds(3028),
            TimeSpan.FromSeconds(3226)
        };
        var actual = xplResult.Groups.Single().Entries.Single(entry => entry.ChapterSet.Chapters.Count == expectedTimes.Length);
        Assert.Equal(expectedTimes, actual.ChapterSet.Chapters.Select(static chapter => chapter.StartTime));
    }

    [Fact]
    public async Task PlaylistXmlSampleReturnsParseDiagnosticBecauseItIsNotHddvdXpl()
    {
        var importer = new XplChapterImporter();
        const string xmlText = """
                               <?xml version="1.0" encoding="ISO-8859-1"?>
                               <Chapters>
                                 <EditionEntry>
                                   <ChapterAtom>
                                     <ChapterTimeStart>00:00:00.000</ChapterTimeStart>
                                     <ChapterDisplay>
                                       <ChapterString>Not XPL</ChapterString>
                                     </ChapterDisplay>
                                   </ChapterAtom>
                                 </EditionEntry>
                               </Chapters>
                               """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlText));

        var result = await importer.ImportAsync(new ChapterImportRequest("test.xml", stream), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.XplParseFailed);
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

    public static TheoryData<SampleExpectation> SampleExpectations() =>
    [
        Sample("VPLST000.XPL",
        [
            new("MM", "file:///dvddisc/HVDVD_TS/PEVOB2.MAP", 1, Ms(60041), TimeSpan.Zero, string.Empty, TimeSpan.Zero,
                string.Empty),
            new("feature", "file:///dvddisc/HVDVD_TS/PEVOB3.MAP", 16, Ms(3486000), TimeSpan.Zero, "Chapter 1",
                Ms(3226000), "Chapter 16"),
            new("card", "file:///dvddisc/HVDVD_TS/PEVOB4.MAP", 1, Ms(10000), TimeSpan.Zero, string.Empty, TimeSpan.Zero,
                string.Empty),
            new("logo", "file:///dvddisc/HVDVD_TS/PEVOB5.MAP", 1, Ms(20000), TimeSpan.Zero, string.Empty, TimeSpan.Zero,
                string.Empty)
        ]),

        Sample("VPLST001.XPL",
        [
            new("Feature Presentation", "file:///dvddisc/HVDVD_TS/PEVOB_1.MAP", 29, Ms(6170933), TimeSpan.Zero,
                "Chapter 1", Ms(6018000), "Chapter 29")
        ]),
        Sample("VPLST002.XPL",
        [
            new("Main Movie", "file:///dvddisc/HVDVD_TS/FEATURE_1.MAP", 10, Ms(6234000), TimeSpan.Zero, "Chapter  1",
                Ms(5761500), "Chapter 10")
        ]),
        Sample("VPLST003.XPL",
        [
            new("Main Movie", "file:///dvddisc/HVDVD_TS/FEATURE_1.MAP", 19, Ms(7516916), TimeSpan.Zero, "Count To Ten",
                Ms(7041958), "Mission: Honeymoon")
        ]),
        Sample("VPLST004.XPL",
        [
            new("Feature Presentation", "file:///dvddisc/HVDVD_TS/PEVOB_1.MAP", 29, Ms(9440200), TimeSpan.Zero,
                "The Riddle House", Ms(8620000), "End Credits")
        ]),
        Sample("VPLST005.XPL",
        [
            new("mainMovie", "file:///dvddisc/HVDVD_TS/L0_mainMovie.MAP", 20, Ms(9012000), TimeSpan.Zero,
                "mainMovie_ch1", Ms(8197958), "mainMovie_ch20")
        ])
    ];

    public sealed record SampleExpectation(string FileName, OptionExpectation[] Entries);

    public sealed record OptionExpectation(
        string Title,
        string SourceName,
        int ChapterCount,
        TimeSpan Duration,
        TimeSpan FirstTime,
        string FirstName,
        TimeSpan LastTime,
        string LastName);

    private static SampleExpectation Sample(string fileName, OptionExpectation[] entries) =>
        new(fileName, entries);

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static void AssertTimeNear(TimeSpan expected, TimeSpan actual) =>
        Assert.True((actual - expected).Duration() <= TimeSpan.FromMilliseconds(1), $"Expected {expected}, got {actual}.");
}

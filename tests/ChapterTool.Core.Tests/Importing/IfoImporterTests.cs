using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class IfoImporterTests
{
    private readonly ChapterTimeFormatter formatter = new();

    [Fact]
    public async Task Vts05SampleMatchesLegacyChapterTimes()
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).First();
        Assert.Equal(ChapterImportFormat.DvdIfo, info.ImportFormat);
        Assert.Equal("VTS_05_1", info.SourceName);
        Assert.Equal(
            [
                "00:00:00.000",
                "00:17:43.562",
                "00:37:17.001",
                "00:56:27.551",
                "01:12:41.057",
                "01:32:31.813",
                "01:49:12.679"
            ],
            info.Chapters.Select(chapter => formatter.Format(chapter.StartTime)));
    }

    [Fact]
    public async Task Vts33SampleImportsHighTitleNumber()
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_33_0.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var infos = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).ToArray();
        Assert.Equal(3, infos.Length);
        Assert.All(infos, info =>
        {
            Assert.StartsWith("VTS_33_", info.SourceName, StringComparison.Ordinal);
            Assert.Equal(47, info.Chapters.Count);
            Assert.Equal(25, info.FramesPerSecond);
            Assert.Equal(TimeSpan.FromMilliseconds(1411200), info.Duration);
            Assert.Equal(TimeSpan.Zero, info.Chapters[0].StartTime);
            Assert.Equal(TimeSpan.FromMinutes(23), info.Chapters[^1].StartTime);
        });
        Assert.Equal(["VTS_33_1", "VTS_33_2", "VTS_33_3"], infos.Select(static info => info.SourceName));
    }

    [Fact]
    public async Task ImportAsyncReadsRequestContentStream()
    {
        var importer = new IfoChapterImporter();
        var path = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO");
        await using var content = File.OpenRead(path);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(path, content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).First();
        Assert.Equal("VTS_05_1", info.SourceName);
        Assert.Equal(7, info.Chapters.Count);
    }

    [Theory]
    [InlineData("NULL.IFO", "NoChaptersFound")]
    [InlineData("OUT_OF_RANGE.IFO", "InvalidIfo")]
    public async Task InvalidIfoSamplesReturnExpectedDiagnostics(string fileName, string expectedCode)
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", fileName)),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}

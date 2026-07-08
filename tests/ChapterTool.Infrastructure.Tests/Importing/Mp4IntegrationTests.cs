using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class Mp4IntegrationTests
{
    private static string FixturePath => FixtureResolver.Fixture("Importing", "Media", "Chapter.mp4");

    [Fact]
    public async Task AtlReader_reads_chapters_from_real_file()
    {
        var reader = new AtlMp4ChapterReader();

        var result = await reader.ReadAsync(FixturePath, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(3, result.Chapters.Count);
        Assert.Equal(
            ["Chapter 01", "Chapter 02", "Chapter 03"],
            result.Chapters.Select(static chapter => chapter.Tags["title"]));
        Assert.Equal(
            [0L, 10000L, 20000L],
            result.Chapters.Select(static chapter => chapter.Start));
        Assert.Equal(
            [10000L, 20000L, 29150L],
            result.Chapters.Select(static chapter => chapter.End));
    }

    [Fact]
    public async Task Importer_with_real_reader_converts_durations_to_cumulative_starts()
    {
        var importer = new MediaChapterImporter(new AtlMp4ChapterReader(), [".mp4", ".m4a", ".m4v"]);

        var result = await importer.ImportAsync(new ChapterImportRequest(FixturePath), TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)],
            result.Groups.Single().Options.Single().ChapterInfo.Chapters.Select(static chapter => chapter.Time));
        Assert.Contains(result.Groups.Single().Options.Single().MediaReferences ?? [],
            reference => reference.AbsolutePath == FixturePath);
    }

    [Fact]
    public async Task AtlReader_returns_empty_for_nonexistent_file()
    {
        var reader = new AtlMp4ChapterReader();

        var result = await reader.ReadAsync(Path.Combine(Path.GetTempPath(), "nonexistent.mp4"), TestContext.Current.CancellationToken);

        // ATL catches FileNotFoundException internally and returns zero chapters
        Assert.True(result.Success);
        Assert.Empty(result.Chapters);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AtlReader_reports_invalid_path(string path)
    {
        var reader = new AtlMp4ChapterReader();

        var result = await reader.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Mp4InvalidPath", result.DiagnosticCode);
    }
}

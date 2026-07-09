using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ChapterSegmentServiceTests
{
    [Fact]
    public void CombineOffsetsMplsSegmentsAndRenumbers()
    {
        var group = new ChapterImportSource(
            "playlist",
            [
                new ChapterImportEntry("a", "a", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A"), new Chapter(2, TimeSpan.FromSeconds(10), "B"))),
                new ChapterImportEntry("b", "b", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(30), new Chapter(1, TimeSpan.Zero, "C"), new Chapter(2, TimeSpan.FromSeconds(5), "D")))
            ]);

        var result = ChapterSegmentService.Combine(group);

        Assert.Empty(result.Diagnostics);
        Assert.Equal("FULL Chapter", result.ChapterSet.Title);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(25)], result.ChapterSet.Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"], result.ChapterSet.Chapters.Select(chapter => chapter.Name));
        Assert.Equal(TimeSpan.FromSeconds(50), result.ChapterSet.Duration);
    }

    [Fact]
    public void CombineRejectsUnsupportedSource()
    {
        var group = new ChapterImportSource("x", [new ChapterImportEntry("x", "x", Info(ChapterImportFormat.Cue, TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "A")))]);

        var result = ChapterSegmentService.Combine(group);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedCombineSource");
    }

    [Fact]
    public void CombineRejectsMixedSupportedSources()
    {
        var group = new ChapterImportSource(
            "mixed",
            [
                new ChapterImportEntry("a", "a", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "A"))),
                new ChapterImportEntry("b", "b", Info(ChapterImportFormat.DvdIfo, TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "B")))
            ]);

        var result = ChapterSegmentService.Combine(group);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedCombineSource");
    }

    [Fact]
    public void AppendCombinesMplsGroups()
    {
        var existing = new ChapterImportSource(
            "a",
            [new ChapterImportEntry("a", "a", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A")))]);
        var appended = new ChapterImportSource(
            "b",
            [new ChapterImportEntry("b", "b", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(10), new Chapter(1, TimeSpan.Zero, "B")))]);

        var result = ChapterSegmentService.Append(existing, appended);

        Assert.Empty(result.Diagnostics);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(20)], result.ChapterSet.Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(TimeSpan.FromSeconds(30), result.ChapterSet.Duration);
    }

    [Fact]
    public void AppendRejectsNonMplsGroups()
    {
        var existing = new ChapterImportSource(
            "a",
            [new ChapterImportEntry("a", "a", Info(ChapterImportFormat.Mpls, TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A")))]);
        var appended = new ChapterImportSource(
            "b",
            [new ChapterImportEntry("b", "b", Info(ChapterImportFormat.DvdIfo, TimeSpan.FromSeconds(10), new Chapter(1, TimeSpan.Zero, "B")))]);

        var result = ChapterSegmentService.Append(existing, appended);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedAppendSource");
    }

    private static ChapterSet Info(ChapterImportFormat sourceType, TimeSpan duration, params Chapter[] chapters) =>
        new(ChapterImportFormats.DisplayName(sourceType), ChapterImportFormats.DisplayName(sourceType), sourceType, 24, duration, chapters);
}

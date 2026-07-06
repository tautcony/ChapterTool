using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ChapterSegmentServiceTests
{
    [Fact]
    public void CombineOffsetsMplsSegmentsAndRenumbers()
    {
        var group = new ChapterInfoGroup(
            "playlist",
            [
                new ChapterSourceOption("a", "a", Info("MPLS", TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A"), new Chapter(2, TimeSpan.FromSeconds(10), "B"))),
                new ChapterSourceOption("b", "b", Info("MPLS", TimeSpan.FromSeconds(30), new Chapter(1, TimeSpan.Zero, "C"), new Chapter(2, TimeSpan.FromSeconds(5), "D")))
            ]);

        var result = ChapterSegmentService.Combine(group);

        Assert.Empty(result.Diagnostics);
        Assert.Equal("FULL Chapter", result.ChapterInfo.Title);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(25)], result.ChapterInfo.Chapters.Select(chapter => chapter.Time));
        Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"], result.ChapterInfo.Chapters.Select(chapter => chapter.Name));
        Assert.Equal(TimeSpan.FromSeconds(50), result.ChapterInfo.Duration);
    }

    [Fact]
    public void CombineRejectsUnsupportedSource()
    {
        var group = new ChapterInfoGroup("x", [new ChapterSourceOption("x", "x", Info("CUE", TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "A")))]);

        var result = ChapterSegmentService.Combine(group);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedCombineSource");
    }

    [Fact]
    public void CombineRejectsMixedSupportedSources()
    {
        var group = new ChapterInfoGroup(
            "mixed",
            [
                new ChapterSourceOption("a", "a", Info("MPLS", TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "A"))),
                new ChapterSourceOption("b", "b", Info("DVD", TimeSpan.FromSeconds(1), new Chapter(1, TimeSpan.Zero, "B")))
            ]);

        var result = ChapterSegmentService.Combine(group);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedCombineSource");
    }

    [Fact]
    public void AppendCombinesMplsGroups()
    {
        var existing = new ChapterInfoGroup(
            "a",
            [new ChapterSourceOption("a", "a", Info("MPLS", TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A")))]);
        var appended = new ChapterInfoGroup(
            "b",
            [new ChapterSourceOption("b", "b", Info("MPLS", TimeSpan.FromSeconds(10), new Chapter(1, TimeSpan.Zero, "B")))]);

        var result = ChapterSegmentService.Append(existing, appended);

        Assert.Empty(result.Diagnostics);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(20)], result.ChapterInfo.Chapters.Select(chapter => chapter.Time));
        Assert.Equal(TimeSpan.FromSeconds(30), result.ChapterInfo.Duration);
    }

    [Fact]
    public void AppendRejectsNonMplsGroups()
    {
        var existing = new ChapterInfoGroup(
            "a",
            [new ChapterSourceOption("a", "a", Info("MPLS", TimeSpan.FromSeconds(20), new Chapter(1, TimeSpan.Zero, "A")))]);
        var appended = new ChapterInfoGroup(
            "b",
            [new ChapterSourceOption("b", "b", Info("DVD", TimeSpan.FromSeconds(10), new Chapter(1, TimeSpan.Zero, "B")))]);

        var result = ChapterSegmentService.Append(existing, appended);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedAppendSource");
    }

    private static ChapterInfo Info(string sourceType, TimeSpan duration, params Chapter[] chapters) =>
        new(sourceType, sourceType, 0, sourceType, 24, duration, chapters);
}

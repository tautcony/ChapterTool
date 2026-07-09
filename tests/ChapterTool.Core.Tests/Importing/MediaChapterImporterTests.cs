using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MediaChapterImporterTests
{
    [Fact]
    public async Task ImportAsyncMapsOrderedStartsEndsDurationAndUnicodeTitles()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/1000", 0, 5000, "0.000000", "5.000000", ("title", "Chapter 01")),
            Entry(1, 1, "1/1000", 5000, 12000, "5.000000", "12.000000", ("title", "Chapter 02")),
            Entry(2, 2, "1/1000", 12000, 20000, "12.000000", "20.000000", ("title", "章节 03")),
            Entry(3, 3, "1/1000", 20000, 30000, "20.000000", "30.000000", ("title", "Chapter 04")));

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mp4"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.Media, info.ImportFormat);
        Assert.Equal(0, info.FramesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(30), info.Duration);
        Assert.Equal(["Chapter 01", "Chapter 02", "章节 03", "Chapter 04"], info.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(20)], info.Chapters.Select(static chapter => chapter.Time));
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)], info.Chapters.Select(static chapter => chapter.End));
    }

    [Fact]
    public async Task ImportAsyncFallsBackToTimeBaseAndTitleFallbacks()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/44100", 0, 220500, "0.000000", "5.000000", ("title", "Start with decimals, end with decimals")),
            Entry(1, 1, "1/44100", 441000, 661500, null, null, ("title", "Time base fallback only")),
            Entry(2, 2, "1/1000", 20000, 35000, "20.000000", "35.000000", ("TITLE", "Uppercase TITLE tag")),
            Entry(3, 3, "1/1000", 35000, 40000, "35.000000", "40.000000"));

        var result = await importer.ImportAsync(new ChapterImportRequest("audio.ogg"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(35)], chapters.Select(static chapter => chapter.Time));
        Assert.Equal(["Start with decimals, end with decimals", "Time base fallback only", "Uppercase TITLE tag", "Chapter 04"], chapters.Select(static chapter => chapter.Name));
    }

    [Fact]
    public async Task ImportAsyncPreservesMissingAndNonContiguousEnds()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/1000", 0, 8000, "0.000000", "8.000000", ("title", "Overlaps next")),
            Entry(1, 1, "1/1000", 5000, 15000, "5.000000", "15.000000", ("title", "Starts before previous ends")),
            Entry(2, 2, "1/1000", 20000, null, "20.000000", null, ("title", "Missing end")),
            Entry(3, 3, "1/1000", 35000, 35000, "35.000000", "35.000000", ("title", "End equals start")));

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.nut"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
        Assert.Equal(TimeSpan.FromSeconds(8), chapters[0].End);
        Assert.Equal(TimeSpan.FromSeconds(15), chapters[1].End);
        Assert.Null(chapters[2].End);
        Assert.Null(chapters[3].End);
        Assert.Equal(TimeSpan.FromSeconds(15), result.Groups.Single().Entries.Single().ChapterSet.Duration);
    }

    [Fact]
    public async Task ImportAsyncFailsEmptyChapterOutput()
    {
        var importer = CreateImporter();

        var result = await importer.ImportAsync(new ChapterImportRequest("empty.wav"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "NoChaptersFound");
    }

    [Fact]
    public async Task ImportAsyncSkipsInvalidStartsAndFailsWhenNoneRemain()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/1000", -1000, 1000, "-1.000000", "1.000000", ("title", "Negative")),
            Entry(1, 1, "bad", 10, 20, null, null, ("title", "Bad time base")));

        var result = await importer.ImportAsync(new ChapterImportRequest("bad.mp3"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "InvalidChapterTimestamp");
    }

    [Fact]
    public async Task ImportAsyncSkipsTimestampsBeyondTimeSpanRange()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/1000", 0, 1000, "999999999999999999999", "1000000000000000000000", ("title", "Huge decimal")),
            Entry(1, 1, "999999999999999999999/1", long.MaxValue, long.MaxValue, null, null, ("title", "Huge time base")));

        var result = await importer.ImportAsync(new ChapterImportRequest("huge.mp4"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "InvalidChapterTimestamp");
    }

    [Fact]
    public async Task ImportAsyncGroupsEditionsByEditionUidWithUntaggedLast()
    {
        var importer = CreateImporter(
            Entry(0, 0, "1/1000", 0, 10000, "0.000000", "10.000000", ("title", "Tagged Chapter 1"), ("EDITION_UID", "100")),
            Entry(1, 1, "1/1000", 10000, 20000, "10.000000", "20.000000", ("title", "Tagged Chapter 2"), ("EDITION_UID", "100")),
            Entry(2, 2, "1/1000", 0, 5000, "0.000000", "5.000000", ("title", "Untagged Chapter 1")),
            Entry(3, 3, "1/1000", 5000, 15000, "5.000000", "15.000000", ("title", "Untagged Chapter 2")));

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mkv"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = result.Groups.Single().Entries;
        Assert.Equal(["edition-0", "edition-1"], entries.Select(static entry => entry.Id));
        Assert.Equal(["Edition 01", "Edition 02"], entries.Select(static entry => entry.DisplayName));
        Assert.All(entries, static entry => Assert.False(entry.CanCombine));
        Assert.Equal(["Tagged Chapter 1", "Tagged Chapter 2"], entries[0].ChapterSet.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal(["Untagged Chapter 1", "Untagged Chapter 2"], entries[1].ChapterSet.Chapters.Select(static chapter => chapter.Name));
    }

    private static MediaChapterImporter CreateImporter(params MediaChapterEntry[] entries) =>
        new(new FakeMediaChapterReader(MediaChapterReadResult.Succeeded(entries)));

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));

    private static MediaChapterEntry Entry(
        int sourceOrder,
        int? id,
        string? timeBase,
        long? start,
        long? end,
        string? startTime,
        string? endTime,
        params (string Key, string Value)[] tags) =>
        new(id, timeBase, start, end, startTime, endTime, tags.ToDictionary(static tag => tag.Key, static tag => tag.Value, StringComparer.Ordinal), sourceOrder);

    private sealed class FakeMediaChapterReader(MediaChapterReadResult result) : IMediaChapterReader
    {
        public ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }
}

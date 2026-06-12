using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MediaChapterImporterTests
{
    [Fact]
    public async Task ImportAsyncMapsOrderedStartsEndsDurationAndUnicodeTitles()
    {
        var importer = CreateImporter("ffprobe_chapters_single_edition.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mp4"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Options.Single().ChapterInfo;
        Assert.Equal("MEDIA", info.SourceType);
        Assert.Equal(0, info.FramesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(30), info.Duration);
        Assert.Equal(["Chapter 01", "Chapter 02", "章节 03", "Chapter 04"], info.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(20)], info.Chapters.Select(static chapter => chapter.Time));
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)], info.Chapters.Select(static chapter => chapter.End));
    }

    [Fact]
    public async Task ImportAsyncFallsBackToTimeBaseAndTitleFallbacks()
    {
        var importer = CreateImporter("ffprobe_chapters_time_base_fallback.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("audio.ogg"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(35)], chapters.Select(static chapter => chapter.Time));
        Assert.Equal(["Start with decimals, end with decimals", "Time base fallback only", "Uppercase TITLE tag", "Chapter 04"], chapters.Select(static chapter => chapter.Name));
    }

    [Fact]
    public async Task ImportAsyncPreservesMissingAndNonContiguousEnds()
    {
        var importer = CreateImporter("ffprobe_chapters_non_contiguous.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.nut"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var chapters = result.Groups.Single().Options.Single().ChapterInfo.Chapters;
        Assert.Equal(TimeSpan.FromSeconds(8), chapters[0].End);
        Assert.Equal(TimeSpan.FromSeconds(15), chapters[1].End);
        Assert.Null(chapters[2].End);
        Assert.Null(chapters[3].End);
        Assert.Equal(TimeSpan.FromSeconds(15), result.Groups.Single().Options.Single().ChapterInfo.Duration);
    }

    [Fact]
    public async Task ImportAsyncFailsEmptyChapterOutput()
    {
        var importer = CreateImporter("ffprobe_chapters_empty.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("empty.wav"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "NoChaptersFound");
    }

    [Fact]
    public async Task ImportAsyncSkipsInvalidStartsAndFailsWhenNoneRemain()
    {
        var importer = CreateImporter("ffprobe_chapters_invalid_timestamps.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("bad.mp3"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "InvalidChapterTimestamp");
    }

    [Fact]
    public async Task ImportAsyncGroupsEditionsByEditionUidWithUntaggedLast()
    {
        var importer = CreateImporter("ffprobe_chapters_mixed_edition.json");

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mkv"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var options = result.Groups.Single().Options;
        Assert.Equal(["edition-0", "edition-1"], options.Select(static option => option.Id));
        Assert.Equal(["Edition 01", "Edition 02"], options.Select(static option => option.DisplayName));
        Assert.All(options, static option => Assert.False(option.CanCombine));
        Assert.Equal(["Tagged Chapter 1", "Tagged Chapter 2"], options[0].ChapterInfo.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal(["Untagged Chapter 1", "Untagged Chapter 2"], options[1].ChapterInfo.Chapters.Select(static chapter => chapter.Name));
    }

    private static MediaChapterImporter CreateImporter(string jsonFixtureFileName)
    {
        var fixtureDirectory = Path.Combine(RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Media", "FfprobeJson");
        var jsonPath = Path.Combine(fixtureDirectory, jsonFixtureFileName);
        var json = File.ReadAllText(jsonPath);
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new FakeProcessRunner(new ProcessRunResult(0, json, "", false, false, "ffprobe", [], null)));
        return new MediaChapterImporter(reader);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.Avalonia.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class FakeToolLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(location);
    }

    private sealed class FakeProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }
}

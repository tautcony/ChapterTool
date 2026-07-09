using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Tests;

public sealed class AtlMp4ChapterReaderTests
{
    [Fact]
    public async Task ReaderNormalizesStartEndTimesIntoOrderedMediaEntries()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("Second", 1500, 4500, UseOffset: false),
            new AtlChapterEntry("Intro", 0, 1500, UseOffset: false)));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["Intro", "Second"], result.Chapters.Select(static chapter => chapter.Tags["title"]));
        Assert.Equal([0L, 1500L], result.Chapters.Select(static chapter => chapter.Start));
        Assert.Equal([1500L, 4500L], result.Chapters.Select(static chapter => chapter.End));
    }

    [Fact]
    public async Task ReaderPreservesUnicodeTitlesAndFractionalTiming()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("序章", 0, 1234, UseOffset: false),
            new AtlChapterEntry("Épilogue", 1234, 2500, UseOffset: false)));

        var result = await reader.ReadAsync("movie.m4a", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["序章", "Épilogue"], result.Chapters.Select(static chapter => chapter.Tags["title"]));
        Assert.Equal([0L, 1234L], result.Chapters.Select(static chapter => chapter.Start));
        Assert.Equal([1234L, 2500L], result.Chapters.Select(static chapter => chapter.End));
    }

    [Fact]
    public async Task ReaderReturnsSuccessfulEmptyResultForImporterNoChaptersHandling()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource());

        var result = await reader.ReadAsync("empty.m4v", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Chapters);
    }

    [Fact]
    public async Task ReaderDiagnosesOffsetBasedChaptersAsUnsupported()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("Offset", 0, 1000, UseOffset: true)));

        var result = await reader.ReadAsync("offset.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.Mp4UnsupportedMetadata, result.DiagnosticCode);
    }

    [Theory]
    [InlineData(1000u, 1000u)]
    [InlineData(2000u, 1000u)]
    public async Task ReaderDiagnosesMalformedChapterTiming(uint startTime, uint endTime)
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("Bad", startTime, endTime, UseOffset: false)));

        var result = await reader.ReadAsync("bad.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.Mp4MalformedMetadata, result.DiagnosticCode);
    }

    [Theory]
    [MemberData(nameof(ExceptionDiagnostics))]
    public async Task ReaderMapsAtlAndFileExceptionsToStructuredDiagnostics(Exception exception, ChapterDiagnosticCode expectedCode)
    {
        var reader = new AtlMp4ChapterReader(new ThrowingAtlTrackChapterSource(exception));

        var result = await reader.ReadAsync("broken.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.Contains(exception.Message, result.Message, StringComparison.Ordinal);
    }

    public static TheoryData<Exception, ChapterDiagnosticCode> ExceptionDiagnostics() => new()
    {
        { new FileNotFoundException("missing"), ChapterDiagnosticCode.Mp4FileNotFound },
        { new DirectoryNotFoundException("missing directory"), ChapterDiagnosticCode.Mp4FileNotFound },
        { new UnauthorizedAccessException("denied"), ChapterDiagnosticCode.Mp4FileInaccessible },
        { new IOException("read failed"), ChapterDiagnosticCode.Mp4ReadFailed },
        { new InvalidDataException("malformed"), ChapterDiagnosticCode.Mp4MalformedMetadata },
        { new InvalidOperationException("unsupported"), ChapterDiagnosticCode.Mp4UnsupportedMetadata },
        { new NotSupportedException("not supported"), ChapterDiagnosticCode.Mp4UnsupportedMetadata },
        { new ArgumentException("bad argument"), ChapterDiagnosticCode.Mp4UnsupportedMetadata }
    };

    private sealed class FakeAtlTrackChapterSource(params AtlChapterEntry[] chapters) : IAtlTrackChapterSource
    {
        public IReadOnlyList<AtlChapterEntry> ReadChapters(string path, CancellationToken cancellationToken) => chapters;
    }

    private sealed class ThrowingAtlTrackChapterSource(Exception exception) : IAtlTrackChapterSource
    {
        public IReadOnlyList<AtlChapterEntry> ReadChapters(string path, CancellationToken cancellationToken) => throw exception;
    }
}

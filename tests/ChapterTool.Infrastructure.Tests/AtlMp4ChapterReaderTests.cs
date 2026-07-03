using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Tests;

public sealed class AtlMp4ChapterReaderTests
{
    [Fact]
    public async Task ReaderNormalizesStartEndTimesIntoOrderedDurations()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("Second", 1500, 4500, UseOffset: false),
            new AtlChapterEntry("Intro", 0, 1500, UseOffset: false)));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["Intro", "Second"], result.Chapters.Select(static chapter => chapter.Title));
        Assert.Equal([TimeSpan.FromMilliseconds(1500), TimeSpan.FromMilliseconds(3000)], result.Chapters.Select(static chapter => chapter.Duration));
    }

    [Fact]
    public async Task ReaderPreservesUnicodeTitlesAndFractionalTiming()
    {
        var reader = new AtlMp4ChapterReader(new FakeAtlTrackChapterSource(
            new AtlChapterEntry("序章", 0, 1234, UseOffset: false),
            new AtlChapterEntry("Épilogue", 1234, 2500, UseOffset: false)));

        var result = await reader.ReadAsync("movie.m4a", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["序章", "Épilogue"], result.Chapters.Select(static chapter => chapter.Title));
        Assert.Equal([TimeSpan.FromMilliseconds(1234), TimeSpan.FromMilliseconds(1266)], result.Chapters.Select(static chapter => chapter.Duration));
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
        Assert.Equal("Mp4UnsupportedMetadata", result.DiagnosticCode);
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
        Assert.Equal("Mp4MalformedMetadata", result.DiagnosticCode);
    }

    [Theory]
    [MemberData(nameof(ExceptionDiagnostics))]
    public async Task ReaderMapsAtlAndFileExceptionsToStructuredDiagnostics(Exception exception, string expectedCode)
    {
        var reader = new AtlMp4ChapterReader(new ThrowingAtlTrackChapterSource(exception));

        var result = await reader.ReadAsync("broken.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.Contains(exception.Message, result.Message, StringComparison.Ordinal);
    }

    public static TheoryData<Exception, string> ExceptionDiagnostics() => new()
    {
        { new FileNotFoundException("missing"), "Mp4FileNotFound" },
        { new DirectoryNotFoundException("missing directory"), "Mp4FileNotFound" },
        { new UnauthorizedAccessException("denied"), "Mp4FileInaccessible" },
        { new IOException("read failed"), "Mp4ReadFailed" },
        { new InvalidDataException("malformed"), "Mp4MalformedMetadata" },
        { new InvalidOperationException("unsupported"), "Mp4UnsupportedMetadata" },
        { new NotSupportedException("not supported"), "Mp4UnsupportedMetadata" },
        { new ArgumentException("bad argument"), "Mp4UnsupportedMetadata" }
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

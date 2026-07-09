using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Tests;

public sealed class FfprobeMediaChapterReaderTests
{
    [Fact]
    public async Task ReadAsyncBuildsExpectedFfprobeCommandAndParsesJson()
    {
        var runner = new FakeProcessRunner(SuccessfulJson(
            """
            {
              "chapters": [
                {
                  "id": 2,
                  "time_base": "1/1000",
                  "start": 5000,
                  "start_time": "5.000000",
                  "end": 10000,
                  "end_time": "10.000000",
                  "tags": {
                    "title": "日本語",
                    "EDITION_UID": "100"
                  }
                }
              ]
            }
            """));
        var reader = new FfprobeMediaChapterReader(new FakeToolLocator(new ExternalToolLocation(true, "/tools/ffprobe")), runner);

        var result = await reader.ReadAsync("/media/movie with spaces.mkv", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(runner.LastRequest);
        Assert.Equal("/tools/ffprobe", runner.LastRequest.FileName);
        Assert.Equal(
            ["-v", "quiet", "-print_format", "json", "-show_chapters", "/media/movie with spaces.mkv"],
            runner.LastRequest.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(30), runner.LastRequest.Timeout);

        var chapter = Assert.Single(result.Chapters);
        Assert.Equal(2, chapter.Id);
        Assert.Equal("1/1000", chapter.TimeBase);
        Assert.Equal(5000, chapter.Start);
        Assert.Equal("5.000000", chapter.StartTime);
        Assert.Equal("日本語", chapter.Tags["title"]);
        Assert.Equal("100", chapter.Tags["EDITION_UID"]);
    }

    [Fact]
    public async Task ReadAsyncReturnsMissingDependencyDiagnostic()
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(false, null, ChapterDiagnosticCode.MissingDependency, "ffprobe missing")),
            new FakeProcessRunner(SuccessfulJson("""{"chapters":[]}""")));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.FfprobeMissingDependency, result.DiagnosticCode);
    }

    [Fact]
    public async Task ReadAsyncReturnsCannotStartDiagnostic()
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new ThrowingProcessRunner(new InvalidOperationException("start failed")));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.FfprobeCannotStart, result.DiagnosticCode);
    }

    [Theory]
    [InlineData(true, false, ChapterDiagnosticReason.TimedOut)]
    [InlineData(false, true, ChapterDiagnosticReason.Cancelled)]
    public async Task ReadAsyncMapsTimeoutAndCancellation(bool timedOut, bool cancelled, ChapterDiagnosticReason reason)
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new FakeProcessRunner(new ProcessRunResult(null, "", "", timedOut, cancelled, "ffprobe", [], null)));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(new ChapterDiagnosticCode(ChapterDiagnosticSource.FfprobeProcess, reason), result.DiagnosticCode);
    }

    [Fact]
    public async Task ReadAsyncMapsNonZeroExitWithStderrDetails()
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new FakeProcessRunner(new ProcessRunResult(1, "", "错误", false, false, "ffprobe", [], null)));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.FfprobeProcessFailed, result.DiagnosticCode);
        Assert.Contains("错误", result.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", ChapterDiagnosticSource.FfprobeOutput, ChapterDiagnosticReason.Empty)]
    [InlineData("{not json", ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.ParseFailed)]
    [InlineData("""{"chapters":"not an array"}""", ChapterDiagnosticSource.Ffprobe, ChapterDiagnosticReason.ParseFailed)]
    public async Task ReadAsyncMapsEmptyAndMalformedOutput(string stdout, ChapterDiagnosticSource source, ChapterDiagnosticReason reason)
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new FakeProcessRunner(SuccessfulJson(stdout)));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(new ChapterDiagnosticCode(source, reason), result.DiagnosticCode);
    }

    [Fact]
    public async Task ReadAsyncRejectsTruncatedJsonOutput()
    {
        var reader = new FfprobeMediaChapterReader(
            new FakeToolLocator(new ExternalToolLocation(true, "ffprobe")),
            new FakeProcessRunner(SuccessfulJson("""{"chapters":[]}""") with { OutputTruncated = true }));

        var result = await reader.ReadAsync("movie.mp4", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.FfprobeOutputTruncated, result.DiagnosticCode);
    }

    private static ProcessRunResult SuccessfulJson(string stdout) =>
        new(0, stdout, "", false, false, "ffprobe", [], null);

    private sealed class FakeToolLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
        {
            Assert.Equal("ffprobe", toolId);
            return ValueTask.FromResult(location);
        }
    }

    private sealed class FakeProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }

        public ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return ValueTask.FromResult(result with
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory
            });
        }
    }

    private sealed class ThrowingProcessRunner(Exception exception) : IProcessRunner
    {
        public ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromException<ProcessRunResult>(exception);
    }
}

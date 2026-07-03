using ChapterTool.Core.Importing;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Matroska;

namespace ChapterTool.Infrastructure.Tests;

public sealed class MatroskaChapterImporterTests
{
    private const string ValidXml = """
        <Chapters>
          <EditionEntry>
            <ChapterAtom>
              <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
              <ChapterDisplay><ChapterString>Intro</ChapterString></ChapterDisplay>
            </ChapterAtom>
          </EditionEntry>
          <EditionEntry>
            <ChapterAtom>
              <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
              <ChapterDisplay><ChapterString>Alt</ChapterString></ChapterDisplay>
            </ChapterAtom>
          </EditionEntry>
        </Chapters>
        """;

    private const string UnicodeXml = """
        <Chapters>
          <EditionEntry>
            <ChapterAtom>
              <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
              <ChapterDisplay><ChapterString>序章</ChapterString></ChapterDisplay>
            </ChapterAtom>
          </EditionEntry>
        </Chapters>
        """;

    [Fact]
    public async Task ImportAsyncReturnsMissingToolDiagnostic()
    {
        var importer = NewImporter(location: new ExternalToolLocation(false, null, "MissingDependency", "mkvextract missing"));

        var result = await importer.ImportAsync(new ChapterImportRequest(@"C:\media\movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MatroskaMissingDependency");
    }

    [Fact]
    public async Task ImportAsyncDelegatesStdoutXmlAndPreservesEditions()
    {
        var importer = NewImporter(result: Successful(ValidXml));

        var result = await importer.ImportAsync(new ChapterImportRequest(@"C:\media\movie.mkv"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Groups.Single().Options.Count);
        Assert.Equal("Intro", result.Groups.Single().Options[0].ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task ImportAsyncPreservesNonAsciiStdoutXmlChapterNames()
    {
        var importer = NewImporter(result: Successful(UnicodeXml));

        var result = await importer.ImportAsync(new ChapterImportRequest("/media/movie.mkv"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.Equal("序章", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Theory]
    [InlineData("", "", "MatroskaNoChapters")]
    [InlineData("", "warnings only", "MatroskaProcessFailed")]
    public async Task ImportAsyncFailsEmptyStdout(string stdout, string stderr, string code)
    {
        var importer = NewImporter(result: Successful(stdout, stderr));

        var result = await importer.ImportAsync(new ChapterImportRequest(@"C:\media\movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task ImportAsyncFailsNonZeroExitWithProcessMetadata()
    {
        var importer = NewImporter(result: new ProcessRunResult(
            2,
            "",
            "bad file",
            false,
            false,
            "mkvextract",
            ["chapters", @"C:\media\movie.mkv"],
            @"C:\media"));

        var result = await importer.ImportAsync(new ChapterImportRequest(@"C:\media\movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("MatroskaProcessFailed", diagnostic.Code);
        Assert.Contains("ExitCode: 2", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsyncPreservesNonAsciiStderrDiagnostics()
    {
        var importer = NewImporter(result: new ProcessRunResult(
            2,
            "",
            "错误: 无法读取章节",
            false,
            false,
            "mkvextract",
            ["chapters", "/media/movie.mkv"],
            "/media"));

        var result = await importer.ImportAsync(new ChapterImportRequest("/media/movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("MatroskaProcessFailed", diagnostic.Code);
        Assert.Contains("错误", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsyncFailsTimeout()
    {
        var importer = NewImporter(result: new ProcessRunResult(null, "", "", true, false, "mkvextract", ["chapters", "movie.mkv"], null));

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MatroskaProcessTimedOut");
    }

    [Fact]
    public async Task ImportAsyncFailsCancellation()
    {
        var importer = NewImporter(result: new ProcessRunResult(null, "", "", false, true, "mkvextract", ["chapters", "movie.mkv"], null));

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.mkv"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MatroskaProcessCancelled");
    }

    [Fact]
    public async Task ImportAsyncPassesPathAsSingleArgument()
    {
        var runner = new FakeProcessRunner(Successful(ValidXml));
        var importer = NewImporter(runner: runner);
        const string path = @"C:\media\movie with spaces.mkv";

        await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.NotNull(runner.LastRequest);
        Assert.Equal(["chapters", path], runner.LastRequest.Arguments);
    }

    private static MatroskaChapterImporter NewImporter(
        ExternalToolLocation? location = null,
        ProcessRunResult? result = null,
        FakeProcessRunner? runner = null)
    {
        return new MatroskaChapterImporter(
            new FakeToolLocator(location ?? new ExternalToolLocation(true, "mkvextract")),
            runner ?? new FakeProcessRunner(result ?? Successful(ValidXml)),
            new ChapterTimeFormatter());
    }

    private static ProcessRunResult Successful(string stdout, string stderr = "") =>
        new(0, stdout, stderr, false, false, "mkvextract", ["chapters", "movie.mkv"], null);

    private sealed class FakeToolLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
        {
            Assert.Equal("mkvextract", toolId);
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
}

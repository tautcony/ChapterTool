using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Bdmv;

namespace ChapterTool.Infrastructure.Tests;

public sealed class BdmvChapterImporterTests
{
    [Fact]
    public async Task ImportAsyncReadsMetadataTitleAndParsesEac3toChapterText()
    {
        var root = CreateBdmvRoot(writeMeta: true);
        var playlistDirectory = Path.Combine(root, "BDMV", "PLAYLIST");
        File.Copy(
            Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
            Path.Combine(playlistDirectory, "00001.mpls"));
        var runner = new FakeRunner([
            Success("""
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """),
            new ProcessRunResult(0, "", "status output", false, false, "eac3to", [], null)
        ], ExportText("""
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Opening
            CHAPTER02=00:12:34.567
            CHAPTER02NAME=Middle
            """));
        var importer = NewImporter(runner);
        var progressValues = new List<double>();
        var progress = new ListProgress(progressValues);

        var result = await importer.ImportAsync(new ChapterImportRequest(root, Progress: progress), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal("Disc Title", info.Title);
        Assert.Equal(ChapterImportFormat.Bdmv, info.ImportFormat);
        Assert.Equal("00001.m2ts", info.SourceName);
        Assert.Equal(2, info.Chapters.Count);
        Assert.Equal(["Opening", "Middle"], info.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal(TimeSpan.FromMilliseconds(754567), info.Chapters[1].StartTime);
        Assert.Equal(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(20)), info.Duration);
        Assert.Equal("00001.m2ts", result.Groups.Single().Entries.Single().ReferencedMediaFiles!.Single().DisplayName);
        Assert.Equal(Path.Combine("..", "STREAM", "00001.m2ts"), result.Groups.Single().Entries.Single().ReferencedMediaFiles!.Single().RelativePath);
        Assert.Equal([root, "-showall"], runner.Requests[0].Arguments);
        Assert.Null(runner.Requests[0].WorkingDirectory);
        Assert.Equal([root, "1)", $"1:{runner.ExportedPaths.Single()}", "-showall"], runner.Requests[1].Arguments);
        Assert.Equal(Path.GetTempPath(), runner.Requests[1].WorkingDirectory);
        Assert.False(runner.Requests[1].RedirectOutput);
        Assert.True(runner.Requests[1].CreateNoWindow);
        Assert.Equal(2, runner.Requests.Count);
        Assert.False(File.Exists(runner.ExportedPaths.Single()));
        Assert.Contains(progressValues, value => value is > 0 and < 1);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "Stdout");
    }

    [Fact]
    public async Task ImportAsyncTreatsUnreadableMetadataTitleAsBestEffort()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateBdmvRoot(writeMeta: true);
        var metaPath = Path.Combine(root, "BDMV", "META", "DL", "disc.xml");
        File.SetUnixFileMode(metaPath, UnixFileMode.None);
        try
        {
            File.Copy(
                Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
                Path.Combine(root, "BDMV", "PLAYLIST", "00001.mpls"));
            var runner = new FakeRunner([
                Success("""
                    1) 00001.mpls, 00001.m2ts, 01:00:20
                       - Chapters, 9 chapters
                    """),
                new ProcessRunResult(0, "", "", false, false, "eac3to", [], null)
            ], ExportText("""
                CHAPTER01=00:00:00.000
                CHAPTER01NAME=Opening
                """));
            var importer = NewImporter(runner);

            var result = await importer.ImportAsync(new ChapterImportRequest(root), TestContext.Current.CancellationToken);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
            Assert.Equal(string.Empty, result.Groups.Single().Entries.Single().ChapterSet.Title);
        }
        finally
        {
            File.SetUnixFileMode(metaPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task ImportAsyncFailsMissingDependency()
    {
        var importer = new BdmvChapterImporter(new FakeLocator(new ExternalToolLocation(false, null, "MissingDependency", "missing")), new FakeRunner([]), new ChapterTimeFormatter());

        var result = await importer.ImportAsync(new ChapterImportRequest(CreateBdmvRoot()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MissingDependency");
    }

    [Theory]
    [InlineData("", "DependencyOutputUnrecognized")]
    [InlineData("stderr", "DependencyExecutionFailed")]
    public async Task ImportAsyncDiagnosesBadDependencyOutput(string stderr, string code)
    {
        var resultToReturn = stderr.Length == 0
            ? Success("not a playlist")
            : new ProcessRunResult(0, "", stderr, false, false, "eac3to", [], null);
        var importer = NewImporter(new FakeRunner([resultToReturn]));

        var result = await importer.ImportAsync(new ChapterImportRequest(CreateBdmvRoot()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task ImportAsyncRejectsTruncatedPlaylistOutput()
    {
        var importer = NewImporter(new FakeRunner([
            new ProcessRunResult(
                0,
                """
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """,
                "",
                false,
                false,
                "eac3to",
                [],
                null,
                OutputTruncated: true)
        ]));

        var result = await importer.ImportAsync(new ChapterImportRequest(CreateBdmvRoot()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DependencyOutputTruncated");
    }

    [Fact]
    public async Task ImportAsyncFailsWhenChapterExportIsNotParseable()
    {
        var root = CreateBdmvRoot();
        File.Copy(
            Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
            Path.Combine(root, "BDMV", "PLAYLIST", "00001.mpls"));
        var runner = new FakeRunner([
            Success("""
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """),
            Success("Created file")
        ], ExportText("not chapters"));
        var importer = NewImporter(runner);

        var result = await importer.ImportAsync(new ChapterImportRequest(root), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OgmInvalidFirstLine");
        Assert.Equal(2, runner.Requests.Count);
    }

    [Fact]
    public async Task ImportAsyncFailsWhenChapterExportFileIsMissing()
    {
        var root = CreateBdmvRoot();
        File.Copy(
            Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
            Path.Combine(root, "BDMV", "PLAYLIST", "00001.mpls"));
        var runner = new FakeRunner([
            Success("""
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """),
            Success("Created file")
        ]);
        var importer = NewImporter(runner);

        var result = await importer.ImportAsync(new ChapterImportRequest(root), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DependencyOutputMissing");
    }

    [Fact]
    public async Task ImportAsyncFailsWhenChapterExportProcessFailsEvenIfFileExists()
    {
        var root = CreateBdmvRoot();
        File.Copy(
            Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
            Path.Combine(root, "BDMV", "PLAYLIST", "00001.mpls"));
        var runner = new FakeRunner([
            Success("""
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """),
            new ProcessRunResult(7, "", "export failed", false, false, "eac3to", [], null)
        ], ExportText("""
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Opening
            """));
        var importer = NewImporter(runner);

        var result = await importer.ImportAsync(new ChapterImportRequest(root), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DependencyExecutionFailed");
    }

    [Theory]
    [InlineData(true, false, "DependencyExecutionTimedOut")]
    [InlineData(false, true, "DependencyExecutionCancelled")]
    public async Task ImportAsyncDiagnosesListTimeoutAndCancellation(bool timedOut, bool cancelled, string code)
    {
        var importer = NewImporter(new FakeRunner([
            new ProcessRunResult(null, "", "", timedOut, cancelled, "eac3to", [], null)
        ]));

        var result = await importer.ImportAsync(new ChapterImportRequest(CreateBdmvRoot()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Theory]
    [InlineData(true, false, "DependencyExecutionTimedOut")]
    [InlineData(false, true, "DependencyExecutionCancelled")]
    public async Task ImportAsyncDiagnosesExportTimeoutAndCancellation(bool timedOut, bool cancelled, string code)
    {
        var root = CreateBdmvRoot();
        File.Copy(
            Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls"),
            Path.Combine(root, "BDMV", "PLAYLIST", "00001.mpls"));
        var runner = new FakeRunner([
            Success("""
                1) 00001.mpls, 00001.m2ts, 01:00:20
                   - Chapters, 9 chapters
                """),
            new ProcessRunResult(null, "", "", timedOut, cancelled, "eac3to", [], null)
        ]);
        var importer = NewImporter(runner);

        var result = await importer.ImportAsync(new ChapterImportRequest(root), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task ImportAsyncReturnsDiagnosticWhenListProcessCannotStart()
    {
        var importer = NewImporter(new ThrowingRunner(new InvalidOperationException("cannot start")));

        var result = await importer.ImportAsync(new ChapterImportRequest(CreateBdmvRoot()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DependencyCannotStart");
    }

    private static BdmvChapterImporter NewImporter(IProcessRunner runner) =>
        new(new FakeLocator(new ExternalToolLocation(true, "eac3to")), runner, new ChapterTimeFormatter());

    private static ProcessRunResult Success(string stdout) =>
        new(0, stdout, "", false, false, "eac3to", [], null);

    private static Func<ProcessRunRequest, Task>? ExportText(string text) =>
        request =>
        {
            var exportArgument = request.Arguments.FirstOrDefault(static argument => argument.StartsWith("1:", StringComparison.Ordinal));
            if (exportArgument is not null)
            {
                return File.WriteAllTextAsync(exportArgument[2..], text);
            }

            return Task.CompletedTask;
        };

    private static string CreateBdmvRoot(bool writeMeta = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "BDMV", "PLAYLIST"));
        if (writeMeta)
        {
            var meta = Path.Combine(root, "BDMV", "META", "DL");
            Directory.CreateDirectory(meta);
            File.WriteAllText(Path.Combine(meta, "disc.xml"), "<di:name>Disc Title</di:name>");
        }

        return root;
    }

    private sealed class FakeLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(location);
    }

    private sealed class ListProgress(List<double> values) : IProgress<ChapterLoadProgress>
    {
        public void Report(ChapterLoadProgress value) => values.Add(value.Value);
    }

    private sealed class FakeRunner(IReadOnlyList<ProcessRunResult> results, Func<ProcessRunRequest, Task>? onRun = null) : IProcessRunner
    {
        private int index;

        public List<ProcessRunRequest> Requests { get; } = [];

        public List<string> ExportedPaths { get; } = [];

        public async ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var exportArgument = request.Arguments.FirstOrDefault(static argument => argument.StartsWith("1:", StringComparison.Ordinal));
            if (exportArgument is not null)
            {
                ExportedPaths.Add(exportArgument[2..]);
            }

            if (onRun is not null)
            {
                await onRun(request);
            }

            var result = results[Math.Min(index, results.Count - 1)];
            index++;
            return result with
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory
            };
        }
    }

    private sealed class ThrowingRunner(Exception exception) : IProcessRunner
    {
        public ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromException<ProcessRunResult>(exception);
    }
}

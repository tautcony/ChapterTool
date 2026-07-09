using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Tests.Services;

public sealed class RuntimeChapterLoadServiceTests
{
    [Theory]
    [InlineData(".txt", "CHAPTER01=00:00:00.000\r\nCHAPTER01NAME=Intro\r\n")]
    [InlineData(".vtt", "WEBVTT\r\n\r\n00:00:00.000 --> 00:00:01.000\r\nIntro\r\n")]
    [InlineData(".cue", "FILE \"audio.flac\" WAVE\r\n  TRACK 01 AUDIO\r\n    TITLE \"Intro\"\r\n    INDEX 01 00:00:00\r\n")]
    public async Task RuntimeRoutesTextCueAndWebVttSources(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        try
        {
            var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Single(result.Groups.Single().Entries.Single().ChapterSet.Chapters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeRoutesXmlSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + ".xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,
            """
            <?xml version="1.0"?>
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Intro</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """);
        try
        {
            var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Single(result.Groups.Single().Entries.Single().ChapterSet.Chapters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".m4a")]
    [InlineData(".m4v")]
    public async Task RuntimeRoutesMp4FamilyThroughMediaReader(string extension)
    {
        var fixturePath = Path.Combine(
            RepositoryRoot(),
            "tests",
            "ChapterTool.Infrastructure.Tests",
            "Fixtures",
            "Importing",
            "Media",
            "Chapter.mp4");
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(fixturePath, path);
        try
        {
            var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            var chapters = result.Groups.Single().Entries.Single().ChapterSet.Chapters;
            Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"], chapters.Select(static chapter => chapter.Name));
            Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)], chapters.Select(static chapter => chapter.StartTime));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeSurfacesMp4ReaderFailureDiagnostics()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + ".mp4");
        await File.WriteAllBytesAsync(path, [0]);
        try
        {
            var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == ChapterDiagnosticCode.FfprobeMissingDependency ||
                diagnostic.Code == ChapterDiagnosticCode.FfprobeCannotStart ||
                diagnostic.Code == ChapterDiagnosticCode.FfprobeProcessFailed ||
                diagnostic.Code == ChapterDiagnosticCode.FfprobeParseFailed);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.UnsupportedSource);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeFallsBackWhenPrimaryCannotBeInvoked()
    {
        var path = await CreateTempFileAsync(".mp4");
        var primary = new StubImporter("ffprobe-media", ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.FfprobeMissingDependency, "missing")));
        var fallback = new StubImporter("mp4", SuccessfulImport(path, ChapterImportFormat.Media));
        var registry = new StubRegistry(primary, fallback);
        var service = new RuntimeChapterLoadService(registry);
        try
        {
            var result = await service.LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            Assert.Equal(1, primary.CallCount);
            Assert.Equal(1, fallback.CallCount);
            var fallbackDiagnostic = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.ImporterFallbackUsed);
            Assert.Equal(DiagnosticSeverity.Info, fallbackDiagnostic.Severity);
            Assert.Equal(path, fallbackDiagnostic.Location);
            Assert.Contains("primary=ffprobe-media", fallbackDiagnostic.Details, StringComparison.Ordinal);
            Assert.Contains("fallback=mp4", fallbackDiagnostic.Details, StringComparison.Ordinal);
            Assert.Contains("reason=Ffprobe.MissingDependency", fallbackDiagnostic.Details, StringComparison.Ordinal);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.FfprobeMissingDependency);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimePassesProgressToImporter()
    {
        var path = await CreateTempFileAsync(".txt");
        var importer = new StubImporter("stub", SuccessfulImport(path, ChapterImportFormat.Ogm));
        var registry = new StubRegistry(importer, fallback: null);
        var service = new RuntimeChapterLoadService(registry);
        var progress = new StubProgress();
        try
        {
            var result = await service.LoadAsync(path, progress, TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            Assert.Same(progress, importer.LastRequest?.ProgressReporter);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeDoesNotFallbackAfterInvokedPrimaryFailure()
    {
        var path = await CreateTempFileAsync(".mp4");
        var primary = new StubImporter("ffprobe-media", ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.FfprobeProcessFailed, "process failed")));
        var fallback = new StubImporter("mp4", SuccessfulImport(path, ChapterImportFormat.Media));
        var registry = new StubRegistry(primary, fallback);
        var service = new RuntimeChapterLoadService(registry);
        try
        {
            var result = await service.LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(1, primary.CallCount);
            Assert.Equal(0, fallback.CallCount);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.FfprobeProcessFailed);
            Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.ImporterFallbackUsed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".mkv")]
    [InlineData(".mka")]
    public async Task RuntimeRoutesMatroskaFamilyToMkvextractWithChapterFixture(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(MatroskaFixture(), path);
        try
        {
            var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

            if (result.Success)
            {
                var entries = result.Groups.Single().Entries;
                var chapters = entries[0].ChapterSet.Chapters;
                Assert.Equal(["Intro", "Act 1", "Act 2", "Credits"], chapters.Select(static chapter => chapter.Name));
                Assert.Equal(
                    [TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(330), TimeSpan.FromSeconds(740)],
                    chapters.Select(static chapter => chapter.StartTime));

                if (result.Diagnostics.Any(static diagnostic => diagnostic.Code == ChapterDiagnosticCode.MatroskaMissingDependency))
                {
                    Assert.Single(entries);
                    Assert.Equal(
                        [TimeSpan.FromSeconds(29.15), null, null, TimeSpan.FromSeconds(775)],
                        chapters.Select(static chapter => chapter.EndTime));
                    Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.ImporterFallbackUsed);
                    Assert.Equal(ChapterImportFormat.Media, entries.Single().ChapterSet.ImportFormat);
                }
                else
                {
                    Assert.Equal(2, entries.Count);
                    Assert.Equal(
                        [null, null, null, TimeSpan.FromSeconds(775)],
                        chapters.Select(static chapter => chapter.EndTime));
                    Assert.Equal(ChapterImportFormat.MatroskaXml, entries[0].ChapterSet.ImportFormat);
                }
            }
            else
            {
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.MatroskaMissingDependency);
            }

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.UnsupportedSource);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeRoutesBdmvDirectoryToBdmvImporter()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "BDMV", "PLAYLIST"));
        try
        {
            var result = await CreateService().LoadAsync(root, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == ChapterDiagnosticCode.MissingDependency ||
                diagnostic.Code == ChapterDiagnosticCode.DependencyExecutionFailed);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.UnsupportedSource);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeLoadsExistingIfoFixture()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VTS_05_0.IFO");

        var result = await CreateService().LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.DvdIfo, info.ImportFormat);
        Assert.Equal(7, info.Chapters.Count);
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

    private static string MatroskaFixture() =>
        Path.Combine(
            RepositoryRoot(),
            "tests",
            "ChapterTool.Infrastructure.Tests",
            "Fixtures",
            "Importing",
            "Media",
            "Chapter.mkv");

    private static async Task<string> CreateTempFileAsync(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0]);
        return path;
    }

    private static ChapterImportResult SuccessfulImport(string path, ChapterImportFormat sourceType)
    {
        var info = new ChapterSet(Path.GetFileNameWithoutExtension(path), Path.GetFileName(path), sourceType, 0, TimeSpan.Zero, [new Chapter(1, TimeSpan.Zero, "Intro")]);
        return new ChapterImportResult(true, [new ChapterImportSource(path, [new ChapterImportEntry("default", ChapterImportFormats.DisplayName(sourceType), info)])], []);
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));

    private static IChapterLoadService CreateService() =>
        RuntimeImportTestFactory.CreateLoadService();

    private sealed class StubRegistry(IChapterImporter primary, IChapterImporter? fallback) : IChapterImporterRegistry
    {
        public IChapterImporter? Resolve(string path) => primary;

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) =>
            primaryResult.Diagnostics.Any(static diagnostic =>
                diagnostic.Code == ChapterDiagnosticCode.FfprobeMissingDependency ||
                diagnostic.Code == ChapterDiagnosticCode.FfprobeCannotStart ||
                diagnostic.Code == ChapterDiagnosticCode.MatroskaMissingDependency ||
                diagnostic.Code == ChapterDiagnosticCode.MatroskaCannotStart ||
                diagnostic.Code == ChapterDiagnosticCode.FlacEmbeddedCueNotFound)
                ? fallback
                : null;
    }

    private sealed class StubImporter(string id, ChapterImportResult result) : IChapterImporter
    {
        public int CallCount { get; private set; }

        public ChapterImportRequest? LastRequest { get; private set; }

        public string Id { get; } = id;

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubProgress : IChapterImportProgressReporter
    {
        public void Report(ChapterImportProgress progress)
        {
        }
    }
}

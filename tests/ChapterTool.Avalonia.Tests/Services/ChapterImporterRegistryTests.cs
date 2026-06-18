using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests;

public sealed class ChapterImporterRegistryTests
{
    [Fact]
    public async Task RuntimeLoadServiceDispatchesThroughInjectedRegistry()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "ignored");
        var importer = new FakeImporter();
        var registry = new FakeRegistry(importer);
        var service = new RuntimeChapterLoadService(registry);

        try
        {
            var result = await service.LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(path, registry.LastPath);
            Assert.Equal(path, importer.LastRequest?.Path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("movie.txt", "OgmChapterImporter")]
    [InlineData("movie.xml", "XmlChapterImporter")]
    [InlineData("movie.mkv", "MatroskaChapterImporter")]
    [InlineData("movie.mp4", "MediaChapterImporter")]
    [InlineData("movie.m4a", "MediaChapterImporter")]
    [InlineData("movie.m4v", "MediaChapterImporter")]
    [InlineData("movie.mov", "MediaChapterImporter")]
    [InlineData("movie.webm", "MatroskaChapterImporter")]
    public void RuntimeRegistryResolvesImporterBySource(string fileName, string expectedTypeName)
    {
        var registry = CreateRealRegistry();

        var importer = registry.Resolve(fileName);

        Assert.NotNull(importer);
        Assert.Equal(expectedTypeName, importer.GetType().Name);
    }

    [Theory]
    [InlineData("movie.mp4")]
    [InlineData("movie.m4a")]
    [InlineData("movie.m4v")]
    public async Task RuntimeRegistryRoutesMp4FamilyThroughMediaReader(string fileName)
    {
        var fixturePath = Path.Combine(
            RepositoryRoot(),
            "tests",
            "ChapterTool.Infrastructure.Tests",
            "Fixtures",
            "Importing",
            "Media",
            "Chapter.mp4");
        var testPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(testPath)!);
        File.Copy(fixturePath, testPath);
        try
        {
            var registry = CreateRealRegistry();
            var importer = registry.Resolve(testPath);
            var result = await importer!.ImportAsync(new ChapterImportRequest(testPath), TestContext.Current.CancellationToken);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Equal(["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"], result.Groups.Single().Options.Single().ChapterInfo.Chapters.Select(static chapter => chapter.Name));
        }
        finally
        {
            File.Delete(testPath);
        }
    }

    [Fact]
    public void RuntimeRegistryResolvesAtlFallbackForLegacyMp4WhenFfprobeCannotBeInvoked()
    {
        var registry = CreateFakeRegistry(
            ffprobeResult: MediaChapterReadResult.Failed("FfprobeMissingDependency", "missing"),
            mp4Result: Mp4ChapterReadResult.Succeeded(new Mp4ChapterClip("Fallback", TimeSpan.FromSeconds(1))));

        var primary = registry.Resolve("movie.mp4")!;
        var primaryResult = ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "FfprobeMissingDependency", "missing"));
        var fallback = registry.ResolveFallback("movie.mp4", primary, primaryResult);

        Assert.NotNull(fallback);
        Assert.Equal("Mp4ChapterImporter", fallback.GetType().Name);
    }

    [Fact]
    public void RuntimeRegistryResolvesFfprobeFallbackForMatroskaWhenMkvextractCannotBeInvoked()
    {
        var registry = CreateFakeRegistry(
            ffprobeResult: MediaChapterReadResult.Succeeded(new MediaChapterEntry(
                0, "1/1000", 0, 1000, "0.000000", "1.000000",
                new Dictionary<string, string> { ["title"] = "Intro" }, 0)),
            mp4Result: Mp4ChapterReadResult.Failed("Mp4ReadFailed", "failed"));

        var primary = registry.Resolve("movie.mkv")!;
        var primaryResult = ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "MatroskaMissingDependency", "missing"));
        var fallback = registry.ResolveFallback("movie.mkv", primary, primaryResult);

        Assert.NotNull(fallback);
        Assert.Equal("MediaChapterImporter", fallback.GetType().Name);
    }

    [Fact]
    public void RuntimeRegistryResolvesFfprobeFallbackForFlacWithoutEmbeddedCue()
    {
        var registry = CreateFakeRegistry(
            ffprobeResult: MediaChapterReadResult.Succeeded(new MediaChapterEntry(
                0, "1/1000", 0, 1000, "0.000000", "1.000000",
                new Dictionary<string, string> { ["title"] = "Intro" }, 0)),
            mp4Result: Mp4ChapterReadResult.Failed("Mp4ReadFailed", "failed"));

        var primary = registry.Resolve("music.flac")!;
        var primaryResult = ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "FlacEmbeddedCueNotFound", "missing"));
        var fallback = registry.ResolveFallback("music.flac", primary, primaryResult);

        Assert.NotNull(fallback);
        Assert.Equal("MediaChapterImporter", fallback.GetType().Name);
    }

    [Fact]
    public void RuntimeRegistryDoesNotFallbackForInvokedFfprobeFailureOrNonLegacyMp4()
    {
        var registry = CreateFakeRegistry(
            ffprobeResult: MediaChapterReadResult.Failed("FfprobeProcessFailed", "failed"),
            mp4Result: Mp4ChapterReadResult.Succeeded(new Mp4ChapterClip("Fallback", TimeSpan.FromSeconds(1))));

        var mp4Primary = registry.Resolve("movie.mp4")!;
        var invokedFailure = ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "FfprobeProcessFailed", "failed"));
        var movPrimary = registry.Resolve("movie.mov")!;
        var missingFfprobe = ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "FfprobeMissingDependency", "missing"));

        Assert.Null(registry.ResolveFallback("movie.mp4", mp4Primary, invokedFailure));
        Assert.Null(registry.ResolveFallback("movie.mov", movPrimary, missingFfprobe));
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

    private static RuntimeChapterImporterRegistry CreateRealRegistry()
    {
        var composition = new AppCompositionRoot(settingsDirectory: Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N")));
        return (RuntimeChapterImporterRegistry)composition.CreateChapterImporterRegistry();
    }

    private static RuntimeChapterImporterRegistry CreateFakeRegistry(MediaChapterReadResult ffprobeResult, Mp4ChapterReadResult mp4Result)
    {
        return new RuntimeChapterImporterRegistry(
            new ChapterTimeFormatter(),
            new FakeExternalToolLocator(),
            new FakeProcessRunner(),
            new FakeMediaChapterReader(ffprobeResult),
            new FakeMp4ChapterReader(mp4Result));
    }

    private sealed class FakeRegistry(IChapterImporter importer) : IChapterImporterRegistry
    {
        public string? LastPath { get; private set; }

        public IChapterImporter? Resolve(string path)
        {
            LastPath = path;
            return importer;
        }

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => null;
    }

    private sealed class FakeImporter : IChapterImporter
    {
        public ChapterImportRequest? LastRequest { get; private set; }

        public string Id => "fake";

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" };

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var info = new ChapterInfo("title", request.Path, 0, "TEST", 24, TimeSpan.Zero, []);
            var option = new ChapterSourceOption("0", "test", info);
            return ValueTask.FromResult(new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, [option], 0)], []));
        }
    }

    private sealed class FakeExternalToolLocator : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolName, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ExternalToolLocation(false, null, "MissingDependency", toolName));
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProcessRunResult(-1, string.Empty, string.Empty, TimedOut: false, Cancelled: false, request.FileName, request.Arguments, request.WorkingDirectory));
    }

    private sealed class FakeMediaChapterReader(MediaChapterReadResult result) : IMediaChapterReader
    {
        public string? LastPath { get; private set; }

        public ValueTask<MediaChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken)
        {
            LastPath = path;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeMp4ChapterReader(Mp4ChapterReadResult result) : IMp4ChapterReader
    {
        public string? LastPath { get; private set; }

        public ValueTask<Mp4ChapterReadResult> ReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Read(path));

        private Mp4ChapterReadResult Read(string path)
        {
            LastPath = path;
            return result;
        }
    }
}

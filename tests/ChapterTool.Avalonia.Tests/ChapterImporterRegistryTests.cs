using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Platform;

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
            var result = await service.LoadAsync(path, CancellationToken.None);

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
    [InlineData("movie.mp4", "Mp4ChapterImporter")]
    public void RuntimeRegistryResolvesImporterBySource(string fileName, string expectedTypeName)
    {
        var registry = new RuntimeChapterImporterRegistry(
            new ChapterTimeFormatter(),
            new FakeExternalToolLocator(),
            new FakeProcessRunner(),
            new FileSystemNativeDependencyService([]));

        var importer = registry.Resolve(fileName);

        Assert.NotNull(importer);
        Assert.Equal(expectedTypeName, importer.GetType().Name);
    }

    [Fact]
    public void RuntimeChapterLoadServiceDoesNotConstructImporterInfrastructureInsideLoad()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Services", "RuntimeChapterLoadService.cs"));

        Assert.Contains("IChapterImporterRegistry", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessRunner", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new ExternalToolLocator", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileSystemNativeDependencyService", text, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Time_Shift.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private sealed class FakeRegistry(IChapterImporter importer) : IChapterImporterRegistry
    {
        public string? LastPath { get; private set; }

        public IChapterImporter? Resolve(string path)
        {
            LastPath = path;
            return importer;
        }
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
}

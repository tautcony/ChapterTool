using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Matroska;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class MatroskaIntegrationTests : IAsyncLifetime
{
    private ExternalToolLocation? mkvextractLocation;
    private string? skipReason;

    public async ValueTask InitializeAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory))
            .ToArray();

        var locator = new ExternalToolLocator(new ChapterToolSettingsStore(root), pathDirs);
        mkvextractLocation = await locator.LocateAsync("mkvextract", TestContext.Current.CancellationToken);

        if (!mkvextractLocation.Found)
        {
            skipReason = "mkvextract not found. Install MKVToolNix to run this integration test.";
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Importer_reads_chapters_from_real_mkv_file()
    {
        RequireMkvToolNix();

        var importer = new MatroskaChapterImporter(
            new SingleToolLocator(mkvextractLocation!),
            new ProcessRunner(),
            new ChapterTimeFormatter());

        var fixturePath = FixtureResolver.Fixture("Importing", "Media", "Chapter.mkv");
        var result = await importer.ImportAsync(new ChapterImportRequest(fixturePath), TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var entries = result.Groups.Single().Entries;
        Assert.Equal(2, entries.Count);

        var chapters = entries[0].ChapterSet.Chapters;
        Assert.Equal(["Intro", "Act 1", "Act 2", "Credits"], chapters.Select(static chapter => chapter.Name));
        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(330), TimeSpan.FromSeconds(740)],
            chapters.Select(static chapter => chapter.StartTime));
        Assert.Equal(
            [null, null, null, TimeSpan.FromSeconds(775)],
            chapters.Select(static chapter => chapter.EndTime));

        var hiddenEditionChapters = entries[1].ChapterSet.Chapters;
        var hiddenChapter = Assert.Single(hiddenEditionChapters);
        Assert.Equal("A hidden and not enabled chapter.", hiddenChapter.Name);
        Assert.Equal(TimeSpan.FromSeconds(120), hiddenChapter.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(240), hiddenChapter.EndTime);
    }

    [Fact]
    public async Task Importer_returns_error_for_nonexistent_mkv_file()
    {
        RequireMkvToolNix();

        var importer = new MatroskaChapterImporter(
            new SingleToolLocator(mkvextractLocation!),
            new ProcessRunner(),
            new ChapterTimeFormatter());

        var result = await importer.ImportAsync(
            new ChapterImportRequest(Path.Combine(Path.GetTempPath(), "nonexistent.mkv")),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.MatroskaProcessFailed);
    }

    private void RequireMkvToolNix()
    {
        if (skipReason is not null)
        {
            Assert.Skip(skipReason);
        }
    }

    private sealed class SingleToolLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
        {
            Assert.Equal("mkvextract", toolId);
            return ValueTask.FromResult(location);
        }
    }
}

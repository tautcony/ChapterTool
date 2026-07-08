using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests.Services;

public sealed class RuntimeChapterSaveServiceTests
{
    [Fact]
    public async Task RuntimeSaveWritesCueBesideRequestedDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var info = new ChapterInfo(
            "audio",
            "audio.flac",
            0,
            "CUE",
            75,
            TimeSpan.FromMinutes(1),
            [new Chapter(1, TimeSpan.Zero, "Intro")]);
        var service = new RuntimeChapterSaveService(new ChapterExportService(new ChapterTimeFormatter()));

        try
        {
            var result = await service.SaveAsync(info, new ChapterExportOptions(ChapterExportFormat.Cue), directory, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            var path = Path.Combine(directory, "audio.cue");
            Assert.True(File.Exists(path));
            Assert.Contains("TRACK 01 AUDIO", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeSavePreservesExporterDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var info = new ChapterInfo(
            "test",
            "test.xml",
            0,
            "XML",
            24,
            TimeSpan.FromMinutes(1),
            [
                new Chapter(1, TimeSpan.Zero, "Chapter 01"),
                new Chapter(2, TimeSpan.FromSeconds(30), "Chapter 02")
            ]);
        var service = new RuntimeChapterSaveService(new ChapterExportService(new ChapterTimeFormatter()));

        try
        {
            var result = await service.SaveAsync(
                info,
                new ChapterExportOptions(ChapterExportFormat.Xml, ProjectOutput: true, OrderShift: -5),
                directory,
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.True(result.Diagnostics.Count >= 2, $"Expected at least 2 diagnostics but got {result.Diagnostics.Count}");
            Assert.Contains(result.Diagnostics, d => d.Code == "OrderShiftNormalized");
            Assert.Contains(result.Diagnostics, d => d.Code == "Saved");
            var savedDiagnostic = result.Diagnostics.Single(d => d.Code == "Saved");
            Assert.Equal(DiagnosticSeverity.Info, savedDiagnostic.Severity);
            Assert.Contains("test.xml", savedDiagnostic.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

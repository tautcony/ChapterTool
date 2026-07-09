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
        var info = new ChapterSet(
            "audio",
            "audio.flac",
            ChapterImportFormat.Cue,
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
        var info = new ChapterSet(
            "test",
            "test.xml",
            ChapterImportFormat.MatroskaXml,
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

    [Fact]
    public async Task RuntimeSaveHonorsUtf8BomOption()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var info = new ChapterSet(
            "test",
            "test.txt",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.FromMinutes(1),
            [new Chapter(1, TimeSpan.Zero, "Chapter 01")]);
        var service = new RuntimeChapterSaveService(new ChapterExportService(new ChapterTimeFormatter()));

        try
        {
            await service.SaveAsync(
                info,
                new ChapterExportOptions(ChapterExportFormat.Txt, EmitBom: true),
                directory,
                TestContext.Current.CancellationToken);
            var withBom = await File.ReadAllBytesAsync(Path.Combine(directory, "test.txt"), TestContext.Current.CancellationToken);

            await service.SaveAsync(
                info,
                new ChapterExportOptions(ChapterExportFormat.Txt, EmitBom: false),
                directory,
                TestContext.Current.CancellationToken);
            var withoutBom = await File.ReadAllBytesAsync(Path.Combine(directory, "test.txt"), TestContext.Current.CancellationToken);

            byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
            Assert.True(withBom.Take(3).SequenceEqual(utf8Bom));
            Assert.False(withoutBom.Take(3).SequenceEqual(utf8Bom));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeSaveReturnsDiagnosticWhenFileSystemWriteFails()
    {
        var info = new ChapterSet(
            "test",
            "test.xml",
            ChapterImportFormat.MatroskaXml,
            24,
            TimeSpan.FromMinutes(1),
            [new Chapter(1, TimeSpan.Zero, "Chapter 01")]);
        var service = new RuntimeChapterSaveService(new ChapterExportService(new ChapterTimeFormatter()));

        var result = await service.SaveAsync(
            info,
            new ChapterExportOptions(ChapterExportFormat.Txt),
            "bad\0path",
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SaveFailed");
    }
}

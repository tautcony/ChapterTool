using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterSaveService(ChapterExportService exporter) : IChapterSaveService
{
    public async ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken)
    {
        var result = exporter.Export(info, options);
        if (!result.Success)
        {
            return result;
        }

        var targetDirectory = string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
        Directory.CreateDirectory(targetDirectory);
        var baseName = string.IsNullOrWhiteSpace(info.SourceName) ? "chapters" : Path.GetFileNameWithoutExtension(info.SourceName);
        var path = Path.Combine(targetDirectory, baseName + result.FileExtension);
        await File.WriteAllTextAsync(path, result.Content, cancellationToken);
        return result with
        {
            Diagnostics = [.. result.Diagnostics, new ChapterDiagnostic(DiagnosticSeverity.Info, "Saved", path)]
        };
    }
}

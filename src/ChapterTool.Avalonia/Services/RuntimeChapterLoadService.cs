using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterLoadService(IChapterImporterRegistry importerRegistry) : IChapterLoadService
{
    public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidPath", "The source path does not exist.")));
        }

        var extension = Path.GetExtension(path);
        var importer = importerRegistry.Resolve(path);

        return importer is null
            ? ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedSource", $"Unsupported source extension: {extension}.")))
            : importer.ImportAsync(new ChapterImportRequest(path), cancellationToken);
    }
}

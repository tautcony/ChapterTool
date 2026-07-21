using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Infrastructure.Importing.Runtime;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterLoadService(IChapterImporterRegistry importerRegistry) : IChapterLoadService
{
    public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
    {
        return LoadAsync(path, progress: null, cancellationToken);
    }

    public ValueTask<ChapterImportResult> LoadAsync(string path, IChapterImportProgressReporter? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidPath, "The source path does not exist.")));
        }

        var extension = Path.GetExtension(path);
        var importer = importerRegistry.Resolve(path);

        return importer is null
            ? ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.UnsupportedSource, $"Unsupported source extension: {extension}.",
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["extension"] = extension })))
            : LoadWithFallbackAsync(path, importer, progress, cancellationToken);
    }

    private async ValueTask<ChapterImportResult> LoadWithFallbackAsync(
        string path,
        IChapterImporter importer,
        IChapterImportProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        var primaryResult = await importer.ImportAsync(new ChapterImportRequest(path, ProgressReporter: progress), cancellationToken);
        if (primaryResult.Success)
        {
            return primaryResult;
        }

        var fallback = importerRegistry.ResolveFallback(path, importer, primaryResult);
        if (fallback is null)
        {
            return primaryResult;
        }

        var fallbackResult = await fallback.ImportAsync(new ChapterImportRequest(path, ProgressReporter: progress), cancellationToken);
        var fallbackReason = primaryResult.Diagnostics.FirstOrDefault();
        var fallbackDiagnostic = new ChapterDiagnostic(
            DiagnosticSeverity.Info,
            ChapterDiagnosticCode.ImporterFallbackUsed,
            $"Primary importer '{importer.Id}' could not be invoked; used fallback importer '{fallback.Id}'.",
            path,
            $"primary={importer.Id}; fallback={fallback.Id}; reason={fallbackReason?.DisplayCode ?? "Unknown"}");
        var diagnostics = primaryResult.Diagnostics.Concat([fallbackDiagnostic]).Concat(fallbackResult.Diagnostics).ToList();
        return fallbackResult with { Diagnostics = diagnostics };
    }
}

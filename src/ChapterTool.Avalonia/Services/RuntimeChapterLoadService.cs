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
            : LoadWithFallbackAsync(path, importer, cancellationToken);
    }

    private async ValueTask<ChapterImportResult> LoadWithFallbackAsync(string path, IChapterImporter importer, CancellationToken cancellationToken)
    {
        var primaryResult = await importer.ImportAsync(new ChapterImportRequest(path), cancellationToken);
        if (primaryResult.Success)
        {
            return primaryResult;
        }

        var fallback = importerRegistry.ResolveFallback(path, importer, primaryResult);
        if (fallback is null)
        {
            return primaryResult;
        }

        var fallbackResult = await fallback.ImportAsync(new ChapterImportRequest(path), cancellationToken);
        var fallbackDiagnostic = new ChapterDiagnostic(
            DiagnosticSeverity.Info,
            "ImporterFallbackUsed",
            $"Primary importer '{importer.Id}' could not be invoked; used fallback importer '{fallback.Id}'.");
        var diagnostics = primaryResult.Diagnostics.Concat([fallbackDiagnostic]).Concat(fallbackResult.Diagnostics).ToList();
        return fallbackResult with { Diagnostics = diagnostics };
    }
}

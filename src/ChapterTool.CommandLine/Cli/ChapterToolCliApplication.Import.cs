using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Command-line import helpers shared by inspect and convert.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
    private async Task<CliImportExecution> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        if (!TryValidateInput(inputPath, out var importer, out var failure))
        {
            return failure!;
        }

        var resolvedImporter = importer!;

        var result = await resolvedImporter.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
        if (!result.Success)
        {
            var fallback = importerRegistry.ResolveFallback(inputPath, resolvedImporter, result);
            if (fallback is not null)
            {
                result = await fallback.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
                if (result.Success)
                {
                    var diagnostics = result.Diagnostics.Concat([
                        new ChapterDiagnostic(
                            DiagnosticSeverity.Info,
                            ChapterDiagnosticCode.ImporterFallbackUsed,
                            $"Primary importer '{resolvedImporter.Id}' could not be invoked; fallback importer '{fallback.Id}' was used.")
                    ]).ToList();

                    return new CliImportExecution(true, fallback, result with { Diagnostics = diagnostics });
                }
            }
        }

        return new CliImportExecution(result.Success, resolvedImporter, result);
    }

    private bool TryValidateInput(string inputPath, out IChapterImporter? importer, out CliImportExecution? failure)
    {
        importer = null;
        failure = null;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            failure = CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.MissingInput, localizer.GetString("Cli.Error.InputRequired")));
            return false;
        }
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            failure = CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InputNotFound, localizer.Format("Cli.Error.InputNotFound", new Dictionary<string, object?> { ["path"] = inputPath })));
            return false;
        }
        importer = importerRegistry.Resolve(inputPath);
        if (importer is not null)
        {
            return true;
        }
        failure = CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.UnsupportedInput, localizer.Format("Cli.Error.UnsupportedInput", new Dictionary<string, object?> { ["path"] = inputPath })));
        return false;
    }
}

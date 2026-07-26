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
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.MissingInput, localizer.GetString("Cli.Error.InputRequired")));
        }

        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InputNotFound, localizer.Format("Cli.Error.InputNotFound", new Dictionary<string, object?> { ["path"] = inputPath })));
        }

        var importer = importerRegistry.Resolve(inputPath);
        if (importer is null)
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.UnsupportedInput, localizer.Format("Cli.Error.UnsupportedInput", new Dictionary<string, object?> { ["path"] = inputPath })));
        }

        var result = await importer.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
        if (!result.Success)
        {
            var fallback = importerRegistry.ResolveFallback(inputPath, importer, result);
            if (fallback is not null)
            {
                result = await fallback.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
                if (result.Success)
                {
                    var diagnostics = result.Diagnostics.Concat([
                        new ChapterDiagnostic(
                            DiagnosticSeverity.Info,
                            ChapterDiagnosticCode.ImporterFallbackUsed,
                            $"Primary importer '{importer.Id}' could not be invoked; fallback importer '{fallback.Id}' was used.")
                    ]).ToList();

                    return new CliImportExecution(true, fallback, result with { Diagnostics = diagnostics });
                }
            }
        }

        return new CliImportExecution(result.Success, importer, result);
    }
}

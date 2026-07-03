using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;

namespace ChapterTool.Infrastructure.Importing.Matroska;

public sealed class MatroskaChapterImporter(
    IExternalToolLocator toolLocator,
    IProcessRunner processRunner,
    XmlChapterImporter xmlImporter)
    : IChapterImporter
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    public MatroskaChapterImporter(
        IExternalToolLocator toolLocator,
        IProcessRunner processRunner,
        IChapterTimeFormatter timeFormatter)
        : this(toolLocator, processRunner, new XmlChapterImporter(timeFormatter))
    {
    }

    public string Id => "matroska";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv",
        ".mka"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var location = await toolLocator.LocateAsync("mkvextract", cancellationToken);
        if (!location.Found || string.IsNullOrWhiteSpace(location.Path))
        {
            return ChapterImportResult.Failed(Error(
                "MatroskaMissingDependency",
                location.Message ?? "mkvextract was not found."));
        }

        var processRequest = new ProcessRunRequest(
            location.Path,
            ["chapters", request.Path],
            Path.GetDirectoryName(request.Path),
            DefaultTimeout);
        ProcessRunResult result;
        try
        {
            result = await processRunner.RunAsync(processRequest, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return ChapterImportResult.Failed(Error("MatroskaCannotStart", $"mkvextract could not be started: {exception.Message}"));
        }

        if (result.Cancelled)
        {
            return ChapterImportResult.Failed(ProcessError("MatroskaProcessCancelled", "mkvextract was cancelled.", result));
        }

        if (result.TimedOut)
        {
            return ChapterImportResult.Failed(ProcessError("MatroskaProcessTimedOut", "mkvextract timed out.", result));
        }

        if (result.ExitCode is not 0)
        {
            return ChapterImportResult.Failed(ProcessError("MatroskaProcessFailed", "mkvextract exited with a non-zero code.", result));
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            var code = string.IsNullOrWhiteSpace(result.StandardError) ? "MatroskaNoChapters" : "MatroskaProcessFailed";
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? "mkvextract did not return chapter XML."
                : $"mkvextract wrote stderr without chapter XML: {result.StandardError.Trim()}";
            return ChapterImportResult.Failed(ProcessError(code, message, result));
        }

        return xmlImporter.ImportText(result.StandardOutput, request.Path);
    }

    private static ChapterDiagnostic ProcessError(string code, string message, ProcessRunResult result)
    {
        var stderr = string.IsNullOrWhiteSpace(result.StandardError)
            ? string.Empty
            : $" Stderr: {result.StandardError.Trim()}";
        return Error(code, $"{message}{stderr} Command: {result.FileName} {string.Join(" ", result.Arguments)} ExitCode: {result.ExitCode?.ToString() ?? "<none>"}");
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}

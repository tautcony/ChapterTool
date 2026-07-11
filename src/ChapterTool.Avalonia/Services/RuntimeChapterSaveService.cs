using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterSaveService(ChapterExportService exporter) : IChapterSaveService
{
    public async ValueTask<ChapterExportResult> SaveAsync(
        ChapterSet info,
        ChapterExportOptions options,
        string? directory,
        CancellationToken cancellationToken,
        string? sourcePath = null)
    {
        var result = exporter.Export(info, options);
        if (!result.Success)
        {
            return result;
        }

        if (!ChapterSavePath.TryNormalizeDirectory(directory, out var targetDirectory) || targetDirectory is null)
        {
            return Fail(
                result,
                ChapterDiagnosticCode.InvalidPath,
                "Output directory was not resolved. Set a default save directory in settings or load a source with a valid path.",
                directory);
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var baseName = ChapterSavePath.BuildBaseFileName(info, sourcePath);
            var path = ChapterSavePath.AllocateUniqueFilePath(targetDirectory, baseName, result.FileExtension);
            await File.WriteAllTextAsync(
                path,
                result.Content,
                OutputTextEncodings.Create(options.TextEncoding, options.EmitBom),
                cancellationToken);
            return result with
            {
                Diagnostics =
                [
                    .. result.Diagnostics,
                    new ChapterDiagnostic(
                        DiagnosticSeverity.Info,
                        ChapterDiagnosticCode.Saved,
                        path,
                        Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = path })
                ]
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Fail(
                result,
                ChapterDiagnosticCode.SaveFailed,
                $"Chapter file could not be saved: {exception.Message}",
                directory,
                exception.Message);
        }
    }

    private static ChapterExportResult Fail(
        ChapterExportResult result,
        ChapterDiagnosticCode code,
        string message,
        string? directory,
        string? detail = null)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["directory"] = directory
        };
        if (!string.IsNullOrWhiteSpace(detail))
        {
            arguments["message"] = detail;
        }

        return result with
        {
            Success = false,
            Diagnostics =
            [
                .. result.Diagnostics,
                new ChapterDiagnostic(DiagnosticSeverity.Error, code, message, Arguments: arguments)
            ]
        };
    }
}

using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterSaveService(ChapterExportService exporter) : IChapterSaveService
{
    public async ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken)
    {
        var result = exporter.Export(info, options);
        if (!result.Success)
        {
            return result;
        }

        try
        {
            var targetDirectory = string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
            Directory.CreateDirectory(targetDirectory);
            var baseName = string.IsNullOrWhiteSpace(info.SourceName) ? "chapters" : Path.GetFileNameWithoutExtension(info.SourceName);
            var path = Path.Combine(targetDirectory, baseName + result.FileExtension);
            await File.WriteAllTextAsync(
                path,
                result.Content,
                OutputTextEncodings.Create(options.TextEncoding, options.EmitBom),
                cancellationToken);
            return result with
            {
                Diagnostics = [.. result.Diagnostics, new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.Saved, path,
                    Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = path })]
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return result with
            {
                Success = false,
                Diagnostics =
                [
                    .. result.Diagnostics,
                    new ChapterDiagnostic(
                        DiagnosticSeverity.Error,
                        ChapterDiagnosticCode.SaveFailed,
                        $"Chapter file could not be saved: {exception.Message}",
                        Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["directory"] = directory,
                            ["message"] = exception.Message
                        })
                ]
            };
        }
    }
}

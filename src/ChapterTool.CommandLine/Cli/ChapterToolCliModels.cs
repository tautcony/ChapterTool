using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.CommandLine.Cli;

public sealed record CliInspectRequest(string InputPath);

public sealed record CliConvertRequest(
    string InputPath,
    string Format,
    string? OutputPath,
    bool Stdout,
    int? GroupIndex,
    int? EntryIndex,
    string? EntryId,
    string? XmlLanguage,
    string? SourceFileName,
    double? FrameRate,
    string? Expression = null,
    string? ExpressionPreset = null);

public sealed record CliSelectionResult(bool IsSuccess, ChapterImportEntry? Entry, string Message, IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public static CliSelectionResult Success(ChapterImportEntry entry) => new(true, entry, string.Empty, []);

    public static CliSelectionResult Failure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics) => new(false, null, message, diagnostics);
}

public static class CliInputResolver
{
    public static string? Resolve(string? argumentInput, string? sourceOption) =>
        !string.IsNullOrWhiteSpace(sourceOption)
            ? sourceOption
            : string.IsNullOrWhiteSpace(argumentInput) ? null : argumentInput;
}

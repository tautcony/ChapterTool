using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Importing.Cue;

/// <summary>
/// Imports chapter data from CUE sheet text files.
/// </summary>
public sealed class CueChapterImporter : IChapterImporter
{
    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "cue";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cue"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var bytes = await PortableInputReader.ReadAllBytesAsync(request, cancellationToken);
        if (bytes is null)
        {
            return ChapterImportResult.Failed(PortableInputReader.TooLargeDiagnostic());
        }

        var text = CueTextDecoder.Decode(bytes, out var usedEncodingFallback);
        var result = CueSheetParser.Parse(text, request.Path);
        if (!usedEncodingFallback)
        {
            return result;
        }

        var warning = new ChapterDiagnostic(
            DiagnosticSeverity.Warning,
            ChapterDiagnosticCode.CueEncodingFallback,
            "CUE text is not valid UTF-8. Invalid byte sequences were replaced. The file may use a legacy encoding such as GBK or Shift-JIS.");
        return result with { Diagnostics = [.. result.Diagnostics, warning] };
    }
}

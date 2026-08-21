using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

internal static class TextImportUtilities
{
    /// <summary>
    /// Reads import text. Invalid UTF-8 falls back to a permissive decode.
    /// </summary>
    public static async ValueTask<DecodedImportText> ReadTextAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        byte[] bytes;
        if (request.Content is not null)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }
        else
        {
            bytes = await File.ReadAllBytesAsync(request.Path, cancellationToken);
        }

        var text = CueTextDecoder.Decode(bytes, out var usedEncodingFallback);
        return new DecodedImportText(text, usedEncodingFallback);
    }

    internal static ChapterImportResult WithEncodingFallback(ChapterImportResult result, bool usedEncodingFallback)
    {
        if (!usedEncodingFallback)
        {
            return result;
        }

        var warning = new ChapterDiagnostic(
            DiagnosticSeverity.Warning,
            ChapterDiagnosticCode.TextEncodingFallback,
            "Text is not valid UTF-8. Invalid byte sequences were replaced. The file may use a legacy encoding such as GBK or Shift-JIS.");
        return result with { Diagnostics = [.. result.Diagnostics, warning] };
    }

    /// <summary>
    /// Executes the SingleGroup operation.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="info">The chapter data to process.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult SingleGroup(string path, ChapterSet info)
    {
        var entry = new ChapterImportEntry("default", info.Title, info);
        var group = new ChapterImportSource(path, [entry]);
        return ChapterImportResult.Succeeded(group);
    }
}

internal readonly record struct DecodedImportText(string Text, bool UsedEncodingFallback);

using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Combines and appends chapter segments from multi-part sources.
/// </summary>
public sealed class ChapterSegmentService
{
    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <param name="group">The group value.</param>
    /// <returns>The operation result.</returns>
    public static ChapterEditResult Combine(ChapterImportSource group)
    {
        if (group.Entries.Count == 0)
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "NoSegments", "No chapter segments are available.")]);
        }

        var sourceType = group.Entries[0].ChapterSet.ImportFormat;
        if (sourceType is not (ChapterImportFormat.Mpls or ChapterImportFormat.DvdIfo) || group.Entries.Any(entry => entry.ChapterSet.ImportFormat != sourceType))
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedCombineSource", "Only MPLS and DVD chapter groups can be combined.")]);
        }

        var offset = TimeSpan.Zero;
        var chapters = new List<Chapter>();
        foreach (var entry in group.Entries)
        {
            foreach (var chapter in entry.ChapterSet.Chapters.Where(static chapter => !chapter.IsSeparator))
            {
                chapters.Add(new Chapter(chapters.Count + 1, offset + chapter.StartTime, $"Chapter {chapters.Count + 1:D2}"));
            }

            offset += entry.ChapterSet.Duration;
        }

        var info = new ChapterSet(
            "FULL Chapter",
            null, sourceType,
            group.Entries[0].ChapterSet.FramesPerSecond,
            offset,
            chapters);
        return new ChapterEditResult(info, []);
    }

    /// <summary>
    /// Executes the Append operation.
    /// </summary>
    /// <param name="existing">The existing value.</param>
    /// <param name="appended">The appended value.</param>
    /// <returns>The operation result.</returns>
    public static ChapterEditResult Append(ChapterImportSource existing, ChapterImportSource appended)
    {
        if (existing.Entries.Count == 0 || appended.Entries.Count == 0)
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "NoSegments", "No chapter segments are available.")]);
        }

        if (existing.Entries.Concat(appended.Entries).Any(static entry => entry.ChapterSet.ImportFormat != ChapterImportFormat.Mpls))
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedAppendSource", "Only MPLS chapter groups can be appended.")]);
        }

        var combined = existing with { Entries = existing.Entries.Concat(appended.Entries).ToList() };
        return Combine(combined);
    }

    private static ChapterSet Empty() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}

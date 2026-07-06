using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

public sealed class ChapterSegmentService
{
    public static ChapterEditResult Combine(ChapterInfoGroup group)
    {
        if (group.Options.Count == 0)
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "NoSegments", "No chapter segments are available.")]);
        }

        var sourceType = group.Options[0].ChapterInfo.SourceType;
        if (sourceType is not ("MPLS" or "DVD") || group.Options.Any(option => option.ChapterInfo.SourceType != sourceType))
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedCombineSource", "Only MPLS and DVD chapter groups can be combined.")]);
        }

        var offset = TimeSpan.Zero;
        var chapters = new List<Chapter>();
        foreach (var option in group.Options)
        {
            foreach (var chapter in option.ChapterInfo.Chapters.Where(static chapter => !chapter.IsSeparator))
            {
                chapters.Add(new Chapter(chapters.Count + 1, offset + chapter.Time, $"Chapter {chapters.Count + 1:D2}"));
            }

            offset += option.ChapterInfo.Duration;
        }

        var info = new ChapterInfo(
            "FULL Chapter",
            null,
            0,
            sourceType,
            group.Options[0].ChapterInfo.FramesPerSecond,
            offset,
            chapters);
        return new ChapterEditResult(info, []);
    }

    public static ChapterEditResult Append(ChapterInfoGroup existing, ChapterInfoGroup appended)
    {
        if (existing.Options.Count == 0 || appended.Options.Count == 0)
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "NoSegments", "No chapter segments are available.")]);
        }

        if (existing.Options.Concat(appended.Options).Any(static option => option.ChapterInfo.SourceType != "MPLS"))
        {
            return new ChapterEditResult(Empty(), [new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedAppendSource", "Only MPLS chapter groups can be appended.")]);
        }

        var combined = existing with { Options = existing.Options.Concat(appended.Options).ToList() };
        return Combine(combined);
    }

    private static ChapterInfo Empty() =>
        new(string.Empty, null, 0, string.Empty, 0, TimeSpan.Zero, []);
}

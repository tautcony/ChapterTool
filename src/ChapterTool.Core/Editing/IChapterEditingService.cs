using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

public interface IChapterEditingService
{
    ChapterEditResult EditTime(ChapterInfo info, int index, string text);

    ChapterEditResult EditFrame(ChapterInfo info, int index, string text, decimal framesPerSecond);

    ChapterEditResult Rename(ChapterInfo info, int index, string name);

    ChapterEditResult Delete(ChapterInfo info, IReadOnlySet<int> indexes);

    ChapterEditResult InsertBefore(ChapterInfo info, int index);

    ChapterEditResult ApplyOrderShift(ChapterInfo info, int shift);

    ChapterEditResult ApplyTemplate(ChapterInfo info, string templateText);

    ChapterEditResult ShiftFramesForward(ChapterInfo info, int frames, decimal framesPerSecond);

    ChapterZonesResult CreateZones(ChapterInfo info, IReadOnlySet<int> indexes, decimal framesPerSecond);
}

public sealed record ChapterZonesResult(
    string Zones,
    IReadOnlyList<Diagnostics.ChapterDiagnostic> Diagnostics);

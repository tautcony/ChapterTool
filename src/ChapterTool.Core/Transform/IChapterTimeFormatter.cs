using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public interface IChapterTimeFormatter
{
    string Format(TimeSpan time);

    TimeSpan ParseOrZero(string text);

    TimeParseResult Parse(string text);

    string FormatCue(TimeSpan time);
}

public sealed record TimeParseResult(
    TimeSpan Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);

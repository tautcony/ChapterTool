using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class ChapterRowViewModel(
    Chapter chapter,
    IChapterTimeFormatter formatter,
    int? number = null,
    string? name = null)
{
    public Chapter Chapter { get; } = chapter;

    public int Number { get; } = number ?? chapter.DisplayNumber;

    public string TimeText { get; set; } = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.StartTime);

    public string Name { get; set; } = name ?? chapter.Name;

    public string FramesInfo { get; set; } = chapter.FramesInfo;

    public bool IsFrameAccurate { get; } = chapter.FrameAccuracy == FrameAccuracy.Accurate;

    public bool IsFrameInexact { get; } = chapter.FrameAccuracy == FrameAccuracy.Inexact;

    public bool IsFrameNeutral { get; } = chapter.FrameAccuracy == FrameAccuracy.Neutral;
}

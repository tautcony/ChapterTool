using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class ChapterRowViewModel
{
    public ChapterRowViewModel(
        Chapter chapter,
        IChapterTimeFormatter formatter,
        int? number = null,
        string? name = null)
    {
        Chapter = chapter;
        Number = number ?? chapter.Number;
        TimeText = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.Time);
        Name = name ?? chapter.Name;
        FramesInfo = chapter.FramesInfo;
        IsFrameAccurate = chapter.FrameAccuracy == FrameAccuracy.Accurate;
        IsFrameInexact = chapter.FrameAccuracy == FrameAccuracy.Inexact;
        IsFrameNeutral = chapter.FrameAccuracy == FrameAccuracy.Neutral;
    }

    public Chapter Chapter { get; }

    public int Number { get; }

    public string TimeText { get; set; }

    public string Name { get; set; }

    public string FramesInfo { get; set; }

    public bool IsFrameAccurate { get; }

    public bool IsFrameInexact { get; }

    public bool IsFrameNeutral { get; }
}

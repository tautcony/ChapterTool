namespace ChapterTool.Core.Models;

public sealed record Chapter(
    int Number,
    TimeSpan Time,
    string Name,
    string FramesInfo = "",
    TimeSpan? End = null,
    FrameAccuracy FrameAccuracy = FrameAccuracy.Neutral)
{
    public static readonly TimeSpan SeparatorTime = TimeSpan.MinValue;

    public bool IsSeparator => Time == SeparatorTime;
}

public enum FrameAccuracy
{
    Neutral,
    Accurate,
    Inexact
}

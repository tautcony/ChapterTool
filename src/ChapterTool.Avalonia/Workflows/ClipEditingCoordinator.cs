using ChapterTool.Avalonia.Session;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Workflows;

/// <summary>
/// Applies edit, clip-session, and frame operations through the workspace's single state owner.
/// </summary>
internal sealed class ClipEditingCoordinator(
    ChapterWorkspace workspace,
    IChapterEditingService editingService,
    IFrameRateService frameRateService)
{
    public ChapterSet? CurrentChapterSet => workspace.CurrentChapterSet;

    public bool SelectClip(int index)
    {
        if (workspace.ClipSession is null || index < 0 || index >= workspace.ClipSession.ClipOptions.Count)
        {
            return false;
        }

        workspace.SelectClip(index);
        return workspace.CurrentChapterSet is not null;
    }

    public ClipCombineTransitionResult ToggleCombine()
    {
        if (workspace.ClipSession is null)
        {
            return new ClipCombineTransitionResult(null, new ChapterEditResult(EmptyChapterSet(), []), false, false);
        }

        var transition = ClipSessionTransitions.ToggleCombine(workspace.ClipSession);
        if (transition.Succeeded && transition.Session is not null)
        {
            workspace.ReplaceSession(transition.Session);
        }

        return transition;
    }

    public ChapterEditResult Edit(ChapterSet current, ChapterCellEdit edit, ChapterEditKind kind) =>
        kind switch
        {
            ChapterEditKind.Time => editingService.EditTime(current, edit.Index, edit.Value),
            ChapterEditKind.Name => editingService.Rename(current, edit.Index, edit.Value),
            ChapterEditKind.Frame => editingService.EditFrame(current, edit.Index, edit.Value, (decimal)current.FramesPerSecond),
            _ => new ChapterEditResult(current, [])
        };

    public ChapterEditResult Delete(ChapterSet current, IReadOnlySet<int> indexes) => editingService.Delete(current, indexes);

    public ChapterEditResult InsertBefore(ChapterSet current, int index) => editingService.InsertBefore(current, index);

    public ChapterEditResult ShiftFramesForward(ChapterSet current, int frames) =>
        editingService.ShiftFramesForward(current, frames, (decimal)current.FramesPerSecond);

    public FrameUpdateOutcome UpdateFrames(
        ChapterSet current,
        FrameRateOption requestedOption,
        bool roundFrames,
        decimal tolerance,
        decimal? configuredFrameRate)
    {
        FrameRateDetectionResult? detection = null;
        var appliedOption = requestedOption;
        if (requestedOption.LegacyMplsCode == 0)
        {
            detection = frameRateService.DetectDetailed(current, tolerance);
            appliedOption = detection.Option;
        }

        var frameResult = frameRateService.UpdateFrames(current, appliedOption, roundFrames, tolerance);
        var storedInfo = configuredFrameRate is null
            ? frameResult.Info
            : frameResult.Info with { FramesPerSecond = (double)configuredFrameRate.Value };
        workspace.WriteBackCurrentChapterSet(storedInfo);
        return new FrameUpdateOutcome(frameResult, detection, appliedOption, workspace.CurrentChapterSet ?? storedInfo);
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}

internal enum ChapterEditKind
{
    Time,
    Name,
    Frame
}

internal sealed record FrameUpdateOutcome(
    FrameInfoResult FrameResult,
    FrameRateDetectionResult? Detection,
    FrameRateOption AppliedOption,
    ChapterSet CurrentChapterSet);

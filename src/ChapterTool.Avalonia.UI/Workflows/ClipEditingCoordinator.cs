using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.Workflows;

/// <summary>
/// Applies edit, clip-session, and frame operations through the workspace's single state owner.
/// </summary>
internal sealed class ClipEditingCoordinator(
    ChapterWorkspace workspace,
    IChapterEditingService editingService,
    IFrameRateService frameRateService)
{
    private readonly ChapterContentCandidateBuilder candidateBuilder = new(editingService);

    public ChapterSet? CurrentChapterSet => workspace.CurrentChapterSet;

    public ChapterContentCandidateBuilder CandidateBuilder => candidateBuilder;

    public ChapterContentPreview PrepareCandidate(string operation, Func<EditableChapterDocument, ChapterCandidateBuildResult> build)
    {
        if (workspace.ContentSession is null)
        {
            throw new InvalidOperationException("No chapter content session is active.");
        }

        return ChapterContentOperationSession.Prepare(workspace.ContentSession, operation, build);
    }

    public async ValueTask<TransactionOutcome> ApplyCandidateAsync(
        ChapterContentPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (workspace.ContentSession is null)
        {
            throw new InvalidOperationException("No chapter content session is active.");
        }

        var outcome = await ChapterContentOperationSession.ApplyAsync(workspace.ContentSession, preview, cancellationToken);
        if (outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
        {
            workspace.PublishContentDocument(outcome.Snapshot.Document);
        }

        return outcome;
    }

    public async ValueTask<TransactionOutcome> ExecuteCandidateAsync(
        string operation,
        Func<EditableChapterDocument, ChapterCandidateBuildResult> build,
        CancellationToken cancellationToken = default)
    {
        var preview = PrepareCandidate(operation, build);
        return await ApplyCandidateAsync(preview, cancellationToken);
    }

    public async ValueTask<HistoryNavigationOutcome?> UndoContentAsync(CancellationToken cancellationToken = default)
    {
        if (workspace.ContentSession is null)
        {
            return null;
        }

        var outcome = await workspace.ContentSession.UndoAsync(cancellationToken);
        if (outcome.Kind == HistoryNavigationOutcomeKind.Committed)
        {
            workspace.PublishContentDocument(outcome.Snapshot.Document);
        }

        return outcome;
    }

    public async ValueTask<HistoryNavigationOutcome?> RedoContentAsync(CancellationToken cancellationToken = default)
    {
        if (workspace.ContentSession is null)
        {
            return null;
        }

        var outcome = await workspace.ContentSession.RedoAsync(cancellationToken);
        if (outcome.Kind == HistoryNavigationOutcomeKind.Committed)
        {
            workspace.PublishContentDocument(outcome.Snapshot.Document);
        }

        return outcome;
    }

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
        if (transition is { Succeeded: true, Session: not null })
        {
            workspace.ReplaceSession(transition.Session);
        }

        return transition;
    }

    public ChapterContentPreview Edit(ChapterCellEdit edit, ChapterEditKind kind)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var chapter = snapshot.Document.Tracks.SelectMany(static track => track.Chapters).ElementAtOrDefault(edit.Index);
        if (chapter is null)
        {
            return ChapterContentOperationSession.Prepare(session, "Edit chapter", document =>
                new ChapterCandidateBuildResult(false, document, [], ["The target chapter no longer exists."]));
        }

        var field = kind switch
        {
            ChapterEditKind.Time => ChapterCellField.StartTime,
            ChapterEditKind.Name => ChapterCellField.Name,
            ChapterEditKind.Frame => ChapterCellField.Frame,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return ChapterContentOperationSession.Prepare(session, $"Edit {field}", document =>
            candidateBuilder.EditCell(document, chapter.Id, field, edit.Value, (decimal)(workspace.CurrentChapterSet?.FramesPerSecond ?? 0d)));
    }

    public ChapterContentPreview Delete(IReadOnlySet<int> indexes, ChapterEditingOptions options)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var chapters = snapshot.Document.Tracks.Single().Chapters;
        var targets = indexes.Where(index => index >= 0 && index < chapters.Length).Select(index => chapters[index].Id).ToHashSet();
        return ChapterContentOperationSession.Prepare(session, "Delete chapters", document => candidateBuilder.Delete(document, targets, options));
    }

    public ChapterContentPreview InsertBefore(int index)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var chapters = snapshot.Document.Tracks.Single().Chapters;
        var beforeId = index >= 0 && index < chapters.Length ? chapters[index].Id : (ChapterId?)null;
        return ChapterContentOperationSession.Prepare(session, "Insert chapter", document => candidateBuilder.InsertBefore(document, beforeId));
    }

    public ChapterContentPreview ShiftFramesForward(int frames)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var targets = session.Snapshot.Document.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id).ToHashSet();
        var fps = (decimal)(workspace.CurrentChapterSet?.FramesPerSecond ?? 0d);
        return ChapterContentOperationSession.Prepare(session, "Shift chapter frames", document => candidateBuilder.ShiftFrames(document, targets, frames, fps));
    }

    public FrameUpdateOutcome UpdateFrames(
        ChapterSet current,
        FrameRateOption requestedOption,
        int frameDecimalPlaces,
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

        var frameResult = frameRateService.UpdateFrames(current, appliedOption, frameDecimalPlaces, tolerance);
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

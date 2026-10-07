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

        return ChapterContentOperationSession.Prepare(workspace.ContentSession, operation, document => BuildForSelectedTrack(document, build));
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
        return workspace.ToggleClipStructure();
    }

    public ChapterContentPreview Edit(ChapterCellEdit edit, ChapterEditKind kind, decimal displayFrameRate)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var trackIndex = workspace.CurrentTrackIndex;
        var chapter = trackIndex >= 0 && trackIndex < snapshot.Document.Tracks.Length
            ? snapshot.Document.Tracks[trackIndex].Chapters.ElementAtOrDefault(edit.Index)
            : null;
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
        return PrepareCandidate($"Edit {field}", document =>
            candidateBuilder.EditCell(document, chapter.Id, field, edit.Value, displayFrameRate));
    }

    public ChapterContentPreview Delete(IReadOnlySet<int> indexes, ChapterEditingOptions options)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var chapters = snapshot.Document.Tracks[workspace.CurrentTrackIndex].Chapters;
        var targets = indexes.Where(index => index >= 0 && index < chapters.Length).Select(index => chapters[index].Id).ToHashSet();
        return PrepareCandidate("Delete chapters", document => candidateBuilder.Delete(document, targets, options));
    }

    public ChapterContentPreview InsertBefore(int index)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = session.Snapshot;
        var chapters = snapshot.Document.Tracks[workspace.CurrentTrackIndex].Chapters;
        var beforeId = index >= 0 && index < chapters.Length ? chapters[index].Id : (ChapterId?)null;
        return PrepareCandidate("Insert chapter", document => candidateBuilder.InsertBefore(document, beforeId));
    }

    public ChapterContentPreview ShiftFramesForward(int frames, decimal displayFrameRate)
    {
        var session = workspace.ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var targets = session.Snapshot.Document.Tracks[workspace.CurrentTrackIndex].Chapters.Where(static chapter => chapter.Kind != ChapterKind.Separator).Select(static chapter => chapter.Id).ToHashSet();
        return PrepareCandidate("Shift chapter frames", document => candidateBuilder.ShiftFrames(document, targets, frames, displayFrameRate));
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
        return new FrameUpdateOutcome(frameResult, detection, appliedOption, current);
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);

    private ChapterCandidateBuildResult BuildForSelectedTrack(
        EditableChapterDocument source,
        Func<EditableChapterDocument, ChapterCandidateBuildResult> build)
    {
        var trackIndex = workspace.CurrentTrackIndex;
        if (trackIndex < 0 || trackIndex >= source.Tracks.Length)
        {
            return new ChapterCandidateBuildResult(false, source, [], ["The selected chapter track is unavailable."]);
        }

        var track = source.Tracks[trackIndex];
        var segment = track.Segments.FirstOrDefault();
        var focused = new EditableChapterDocument(source.Id, segment?.Name ?? track.Name,
            segment?.SourceName ?? source.SourceName, segment?.ImportFormat ?? source.ImportFormat,
            EditableChapterDocumentAdapter.TrackDuration(source, trackIndex),
            track.Segments.FirstOrDefault()?.FrameRate ?? source.FrameRate,
            [track]);
        var result = build(focused);
        if (!result.IsValid || result.Candidate.Tracks.Length != 1)
        {
            return result with { Candidate = source };
        }

        var tracks = source.Tracks.SetItem(trackIndex, result.Candidate.Tracks[0]);
        var effectiveRates = tracks
            .SelectMany(static candidateTrack => candidateTrack.Segments.Select(static segment => segment.FrameRate))
            .Where(static rate => rate.HasValue)
            .Select(static rate => rate!.Value)
            .Distinct()
            .ToArray();
        var candidate = new EditableChapterDocument(source.Id, source.Title, source.SourceName,
            source.ImportFormat, EditableChapterDocumentAdapter.ReplacedTrackDuration(source, trackIndex, result.Candidate),
            effectiveRates.Length == 1 ? effectiveRates[0] : effectiveRates.Length == 0 ? source.FrameRate : null,
            tracks);
        return result with { Candidate = candidate };
    }
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

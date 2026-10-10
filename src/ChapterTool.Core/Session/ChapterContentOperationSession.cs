using System.Collections.Immutable;
using System.Globalization;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>Describes one immutable candidate preview captured from a session snapshot.</summary>
public sealed record ChapterContentPreview(
    Guid PreviewId,
    SessionBaseToken BaseToken,
    string Operation,
    EditableChapterDocument Before,
    EditableChapterDocument Candidate,
    ImmutableHashSet<ChapterId> TargetIds,
    ImmutableArray<ChapterContentDifference> Differences,
    ImmutableArray<string> Errors,
    HistoryOperationDescriptor? OperationDescriptor = null)
{
    /// <summary>Gets a value indicating whether the candidate can be applied.</summary>
    public bool IsValid => Errors.IsEmpty;
}

/// <summary>Contains the before and after values for one changed item in a preview.</summary>
public sealed record ChapterContentDifference(
    ChapterId? ChapterId,
    string Field,
    string Before,
    string After);

/// <summary>Builds previews and applies them as one session history transaction.</summary>
public static class ChapterContentOperationSession
{
    /// <summary>Captures the current snapshot and builds a complete candidate preview.</summary>
    public static ChapterContentPreview Prepare(
        SessionState session,
        string operation,
        Func<EditableChapterDocument, ChapterCandidateBuildResult> buildCandidate,
        HistoryOperationDescriptor? operationDescriptor = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(buildCandidate);
        var snapshot = session.Snapshot;
        ChapterCandidateBuildResult result;
        try
        {
            result = buildCandidate(snapshot.Document);
        }
        catch (Exception exception) when (exception is not StackOverflowException)
        {
            result = new ChapterCandidateBuildResult(false, snapshot.Document, [], [exception.Message]);
        }

        var candidate = result.Candidate;
        var errors = result.IsValid
            ? EditableChapterDocumentValidator.Validate(candidate).Errors
            : result.Errors;
        return new ChapterContentPreview(
            Guid.NewGuid(),
            snapshot.BaseToken,
            operation.Trim(),
            snapshot.Document,
            candidate,
            result.TargetIds,
            Diff(snapshot.Document, candidate),
            errors,
            operationDescriptor);
    }

    /// <summary>Applies a valid preview only when its captured base is still current.</summary>
    public static ValueTask<TransactionOutcome> ApplyAsync(
        SessionState session,
        ChapterContentPreview preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.IsValid)
        {
            return ValueTask.FromResult(new TransactionOutcome(
                preview.PreviewId,
                TransactionOutcomeKind.Invalid,
                session.Snapshot,
                preview.Errors));
        }

        return session.ExecuteAsync(
            preview.BaseToken,
            preview.PreviewId,
            Fingerprint(preview),
            (_, _) => ValueTask.FromResult(preview.Candidate),
            cancellationToken,
            preview.Operation,
            preview.OperationDescriptor);
    }

    private static ImmutableArray<ChapterContentDifference> Diff(
        EditableChapterDocument before,
        EditableChapterDocument after)
    {
        var result = ImmutableArray.CreateBuilder<ChapterContentDifference>();
        if (!string.Equals(before.Title, after.Title, StringComparison.Ordinal))
        {
            result.Add(new ChapterContentDifference(null, "Title", before.Title, after.Title));
        }

        if (!string.Equals(before.SourceName, after.SourceName, StringComparison.Ordinal))
        {
            result.Add(new ChapterContentDifference(null, "SourceName", before.SourceName ?? string.Empty, after.SourceName ?? string.Empty));
        }

        var beforeChapters = before.Tracks.SelectMany(static track => track.Chapters).ToDictionary(static chapter => chapter.Id);
        var afterChapters = after.Tracks.SelectMany(static track => track.Chapters).ToDictionary(static chapter => chapter.Id);
        foreach (var id in beforeChapters.Keys.Union(afterChapters.Keys))
        {
            beforeChapters.TryGetValue(id, out var oldValue);
            afterChapters.TryGetValue(id, out var newValue);
            if (oldValue is null)
            {
                result.Add(new ChapterContentDifference(id, "Chapter", string.Empty, Describe(newValue!)));
            }
            else if (newValue is null)
            {
                result.Add(new ChapterContentDifference(id, "Chapter", Describe(oldValue), string.Empty));
            }
            else
            {
                AddIfChanged(result, id, "DisplayNumber", oldValue.DisplayNumber, newValue.DisplayNumber);
                AddIfChanged(result, id, "StartTicks", oldValue.StartTicks, newValue.StartTicks);
                AddIfChanged(result, id, "Name", oldValue.Name, newValue.Name);
                AddIfChanged(result, id, "FramesInfo", oldValue.FramesInfo, newValue.FramesInfo);
                AddIfChanged(result, id, "EndTicks", oldValue.EndTicks, newValue.EndTicks);
                AddIfChanged(result, id, "FrameAccuracy", oldValue.FrameAccuracy, newValue.FrameAccuracy);
                AddIfChanged(result, id, "Kind", oldValue.Kind, newValue.Kind);
            }
        }

        if (!before.Tracks.Select(static track => track.Id).SequenceEqual(after.Tracks.Select(static track => track.Id)))
        {
            result.Add(new ChapterContentDifference(null, "TrackOrder", TrackOrder(before), TrackOrder(after)));
        }

        for (var index = 0; index < Math.Min(before.Tracks.Length, after.Tracks.Length); index++)
        {
            if (!string.Equals(before.Tracks[index].Name, after.Tracks[index].Name, StringComparison.Ordinal))
            {
                result.Add(new ChapterContentDifference(null, $"Track[{index}].Name", before.Tracks[index].Name, after.Tracks[index].Name));
            }
        }

        if (before.Duration != after.Duration)
        {
            AddIfChanged(result, null, "Duration", before.Duration, after.Duration);
        }

        if (before.FrameRate != after.FrameRate)
        {
            AddIfChanged(result, null, "FrameRate", before.FrameRate, after.FrameRate);
        }

        return result.ToImmutable();
    }

    private static void AddIfChanged<T>(ImmutableArray<ChapterContentDifference>.Builder result, ChapterId? id, string field, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
        {
            result.Add(new ChapterContentDifference(id, field, before?.ToString() ?? string.Empty, after?.ToString() ?? string.Empty));
        }
    }

    private static string Describe(EditableChapter chapter) =>
        $"{chapter.DisplayNumber}|{chapter.StartTicks}|{chapter.Name}|{chapter.EndTicks?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}";

    private static string TrackOrder(EditableChapterDocument document) =>
        string.Join(",", document.Tracks.Select(static track => track.Id.Value.ToString("N")));

    private static string Fingerprint(ChapterContentPreview preview) =>
        $"{preview.Operation}:{preview.PreviewId:N}:{preview.BaseToken.StateIdentity:N}:{preview.TargetIds.Count}";
}

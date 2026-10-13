using System.Collections.Immutable;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>Describes one node in a session's retained edit history.</summary>
/// <param name="Id">The node identity.</param>
/// <param name="ParentId">The parent node identity, or <see langword="null"/> for the root.</param>
/// <param name="ChildIds">The child node identities in history order.</param>
/// <param name="PreferredChildId">The preferred child used by redo navigation.</param>
/// <param name="Description">The user-facing operation description.</param>
/// <param name="Operation">The captured operation details, when available.</param>
/// <param name="PublishedAt">The time the node was published.</param>
public sealed record SessionHistoryNodeSnapshot(
    Guid Id,
    Guid? ParentId,
    ImmutableArray<Guid> ChildIds,
    Guid? PreferredChildId,
    string Description,
    HistoryOperationDescriptor? Operation = null,
    DateTimeOffset? PublishedAt = null);

/// <summary>Stable identity and immutable captured parameters for a committed edit.</summary>
/// <param name="Kind">The stable operation kind.</param>
/// <param name="Parameters">The ordered operation parameters.</param>
public sealed record HistoryOperationDescriptor(
    string Kind,
    ImmutableArray<HistoryOperationParameter> Parameters)
{
    /// <summary>Gets the descriptor used when an operation has no specialized details.</summary>
    public static HistoryOperationDescriptor Generic { get; } = new("generic", []);
}

/// <summary>A scalar parameter captured when an operation was prepared.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Value">The captured parameter value, when available.</param>
/// <param name="ValueKind">The kind used to interpret the value.</param>
public sealed record HistoryOperationParameter(string Name, string? Value, string ValueKind = "text");

/// <summary>Presence-aware value in a read-only history detail.</summary>
/// <param name="IsPresent">Whether the field existed in the corresponding document state.</param>
/// <param name="Value">The field value, when present.</param>
public sealed record HistoryFieldValue(bool IsPresent, object? Value);

/// <summary>A stable identity with historical display text for an ordered collection.</summary>
/// <param name="Id">The stable item identity.</param>
/// <param name="Label">The item label captured in the historical state.</param>
public sealed record HistoryOrderItem(string Id, string Label);

/// <summary>A stable track identity with the label captured from that historical document.</summary>
/// <param name="Id">The track identity.</param>
/// <param name="Label">The track label captured in the historical state.</param>
public sealed record HistoryTrackReference(ChapterTrackId Id, string Label);

/// <summary>A typed before/after field projection for a retained history edge.</summary>
/// <param name="Group">The display group for the changed field.</param>
/// <param name="ObjectId">The stable identity of the changed object.</param>
/// <param name="Field">The changed field name.</param>
/// <param name="Before">The field value before the operation.</param>
/// <param name="After">The field value after the operation.</param>
/// <param name="ObjectLabel">The object label captured for display.</param>
/// <param name="Context">Additional display context, when available.</param>
/// <param name="BeforeFrameRate">The frame rate before the operation.</param>
/// <param name="AfterFrameRate">The frame rate after the operation.</param>
public sealed record HistoryFieldChange(
    string Group,
    string ObjectId,
    string Field,
    HistoryFieldValue Before,
    HistoryFieldValue After,
    string ObjectLabel,
    string? Context = null,
    FrameRateValue BeforeFrameRate = default,
    FrameRateValue AfterFrameRate = default);

/// <summary>Lightweight count summary for a retained history node.</summary>
/// <param name="AddedChapters">The number of added chapters.</param>
/// <param name="RemovedChapters">The number of removed chapters.</param>
/// <param name="ModifiedChapters">The number of modified chapters.</param>
/// <param name="ChangedTracks">The number of changed tracks.</param>
/// <param name="ChangedSegments">The number of changed segments.</param>
public sealed record HistoryNodeSummary(int AddedChapters, int RemovedChapters, int ModifiedChapters, int ChangedTracks, int ChangedSegments);

/// <summary>Detailed, parent-relative view of one retained history node.</summary>
/// <param name="RootId">The identity of the history root.</param>
/// <param name="NodeId">The identity of the inspected node.</param>
/// <param name="ParentId">The parent node identity, when present.</param>
/// <param name="Description">The operation description.</param>
/// <param name="Operation">The operation details, when available.</param>
/// <param name="PublishedAt">The publication time, when available.</param>
/// <param name="Summary">The counts of changed content.</param>
/// <param name="Changes">The field-level changes from the parent.</param>
/// <param name="RootDocument">The root document snapshot, when requested.</param>
public sealed record HistoryNodeDetails(Guid RootId, Guid NodeId, Guid? ParentId, string Description,
    HistoryOperationDescriptor? Operation, DateTimeOffset? PublishedAt, HistoryNodeSummary Summary,
    ImmutableArray<HistoryFieldChange> Changes, EditableChapterDocument? RootDocument);

/// <summary>Describes the result of inspecting a history node.</summary>
public enum HistoryInspectionOutcomeKind
{
    /// <summary>The requested history details are available.</summary>
    Available,
    /// <summary>The requested node does not exist.</summary>
    NotFound,
    /// <summary>The document session ended before inspection completed.</summary>
    Ended,
    /// <summary>The inspection was cancelled.</summary>
    Cancelled,
    /// <summary>A required resource was unavailable.</summary>
    ResourceFailure
}

/// <summary>Contains the result and any errors from history inspection.</summary>
/// <param name="Kind">The inspection outcome.</param>
/// <param name="Details">The inspected node details, when available.</param>
/// <param name="Errors">Errors reported while inspecting history.</param>
public sealed record HistoryInspectionOutcome(HistoryInspectionOutcomeKind Kind, HistoryNodeDetails? Details, ImmutableArray<string> Errors);

/// <summary>A stable view of the complete retained history tree.</summary>
/// <param name="RootId">The identity of the root node.</param>
/// <param name="CursorId">The identity of the current history node.</param>
/// <param name="Nodes">The history nodes in display order.</param>
public sealed record SessionHistorySnapshot(
    Guid RootId,
    Guid CursorId,
    ImmutableArray<SessionHistoryNodeSnapshot> Nodes);

/// <summary>Describes the result of undo, redo, or history navigation.</summary>
public enum HistoryNavigationOutcomeKind
{
    /// <summary>The cursor moved and the target document was published.</summary>
    Committed,
    /// <summary>The requested navigation had no target.</summary>
    NoChange,
    /// <summary>The requested history node does not exist.</summary>
    NotFound,
    /// <summary>Resources were exhausted before the target could be published.</summary>
    ResourceFailure,
    /// <summary>The document session ended before navigation could be published.</summary>
    Cancelled
}

/// <summary>The result of a history navigation command.</summary>
/// <param name="Kind">The navigation outcome.</param>
/// <param name="Snapshot">The document snapshot after navigation.</param>
/// <param name="CursorId">The resulting history cursor identity.</param>
/// <param name="Errors">Errors reported while navigating.</param>
public sealed record HistoryNavigationOutcome(
    HistoryNavigationOutcomeKind Kind,
    SessionSnapshot Snapshot,
    Guid CursorId,
    ImmutableArray<string> Errors);

internal enum SessionHistoryFailurePoint
{
    ChangeSetConstruction,
    NodePublication,
    Reconstruction,
    InspectionReconstruction
}

internal sealed record SessionHistoryTree(
    EditableChapterDocument RootDocument,
    Guid RootId,
    Guid CursorId,
    ImmutableDictionary<Guid, SessionHistoryNode> Nodes)
{
    public static SessionHistoryTree Create(EditableChapterDocument document)
    {
        var rootId = Guid.NewGuid();
        var root = new SessionHistoryNode(rootId, null, [], null, "Document loaded", null, new HistoryOperationDescriptor("load", []), DateTimeOffset.UtcNow);
        return new SessionHistoryTree(document, rootId, rootId, ImmutableDictionary<Guid, SessionHistoryNode>.Empty.Add(rootId, root));
    }

    public SessionHistoryTree Append(Guid id, DomainChangeSet changeSet, string description, HistoryOperationDescriptor? operation, DateTimeOffset publishedAt)
    {
        var parent = Nodes[CursorId];
        var updatedParent = parent with
        {
            Children = parent.Children.Add(id),
            PreferredChildId = id
        };
        var node = new SessionHistoryNode(id, CursorId, [], null, description, changeSet, operation, publishedAt);
        return this with { CursorId = id, Nodes = Nodes.SetItem(CursorId, updatedParent).Add(id, node) };
    }

    public SessionHistoryTree MoveTo(Guid cursorId)
    {
        var updatedNodes = Nodes;
        var childId = cursorId;
        while (updatedNodes[childId].ParentId is Guid parentId)
        {
            var parent = updatedNodes[parentId];
            if (parent.PreferredChildId == childId)
            {
                break;
            }

            updatedNodes = updatedNodes.SetItem(parentId, parent with { PreferredChildId = childId });
            childId = parentId;
        }

        return this with { CursorId = cursorId, Nodes = updatedNodes };
    }

    public SessionHistorySnapshot Snapshot()
    {
        var result = ImmutableArray.CreateBuilder<SessionHistoryNodeSnapshot>(Nodes.Count);
        var pending = new Stack<Guid>();
        pending.Push(RootId);
        while (pending.TryPop(out var id))
        {
            var node = Nodes[id];
            result.Add(new SessionHistoryNodeSnapshot(node.Id, node.ParentId, node.Children, node.PreferredChildId, node.Description, node.Operation, node.PublishedAt));
            for (var index = node.Children.Length - 1; index >= 0; index--)
            {
                pending.Push(node.Children[index]);
            }
        }

        return new SessionHistorySnapshot(RootId, CursorId, result.ToImmutable());
    }
}

internal sealed record SessionHistoryNode(
    Guid Id,
    Guid? ParentId,
    ImmutableArray<Guid> Children,
    Guid? PreferredChildId,
    string Description,
    DomainChangeSet? ChangeSet,
    HistoryOperationDescriptor? Operation,
    DateTimeOffset? PublishedAt);

internal sealed record DomainChangeSet(
    DocumentValues BeforeValues,
    DocumentValues AfterValues,
    ImmutableArray<TrackChange> TrackChanges,
    ImmutableArray<ChapterTrackId>? BeforeTrackOrder,
    ImmutableArray<ChapterTrackId>? AfterTrackOrder)
{
    public static DomainChangeSet Create(EditableChapterDocument before, EditableChapterDocument after)
    {
        if (TryCreateValueOnlyChanges(before, after, out var valueOnlyChanges))
        {
            return new DomainChangeSet(
                DocumentValues.From(before),
                DocumentValues.From(after),
                valueOnlyChanges,
                null,
                null);
        }

        var beforeTracks = before.Tracks.ToDictionary(track => track.Id);
        var afterTracks = after.Tracks.ToDictionary(track => track.Id);
        var changes = ImmutableArray.CreateBuilder<TrackChange>();
        foreach (var trackId in beforeTracks.Keys.Union(afterTracks.Keys))
        {
            beforeTracks.TryGetValue(trackId, out var oldTrack);
            afterTracks.TryGetValue(trackId, out var newTrack);
            var change = CreateTrackChange(trackId, oldTrack, newTrack);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        var beforeOrder = before.Tracks.Select(track => track.Id).ToImmutableArray();
        var afterOrder = after.Tracks.Select(track => track.Id).ToImmutableArray();
        var tracksReordered = !beforeOrder.AsSpan().SequenceEqual(afterOrder.AsSpan());
        return new DomainChangeSet(
            DocumentValues.From(before),
            DocumentValues.From(after),
            changes.ToImmutable(),
            tracksReordered ? beforeOrder : null,
            tracksReordered ? afterOrder : null);
    }

    private static TrackChange? CreateTrackChange(ChapterTrackId trackId, EditableChapterTrack? before, EditableChapterTrack? after)
    {
        if (before is null || after is null)
        {
            return new TrackChange(trackId, before, after, null, null, [], null, null, null, null);
        }

        var chapterChanges = CreateChapterChanges(before, after);
        var beforeOrder = before.Chapters.Select(static chapter => chapter.Id).ToImmutableArray();
        var afterOrder = after.Chapters.Select(static chapter => chapter.Id).ToImmutableArray();
        var orderChanged = !beforeOrder.AsSpan().SequenceEqual(afterOrder.AsSpan());
        var hasChanges = !string.Equals(before.Name, after.Name, StringComparison.Ordinal)
            || before.FrameRate != after.FrameRate
            || !before.Segments.SequenceEqual(after.Segments)
            || !chapterChanges.IsEmpty
            || orderChanged;
        return hasChanges
            ? new TrackChange(trackId, null, null, before.Name, after.Name, chapterChanges,
                orderChanged ? beforeOrder : null, orderChanged ? afterOrder : null,
                before.Segments, after.Segments, before.FrameRate, after.FrameRate)
            : null;
    }

    private static ImmutableArray<ChapterChange> CreateChapterChanges(EditableChapterTrack before, EditableChapterTrack after)
    {
        var beforeChapters = before.Chapters.ToDictionary(static chapter => chapter.Id);
        var afterChapters = after.Chapters.ToDictionary(static chapter => chapter.Id);
        var changes = ImmutableArray.CreateBuilder<ChapterChange>();
        foreach (var chapterId in beforeChapters.Keys.Union(afterChapters.Keys))
        {
            beforeChapters.TryGetValue(chapterId, out var oldChapter);
            afterChapters.TryGetValue(chapterId, out var newChapter);
            if (oldChapter != newChapter)
            {
                changes.Add(new ChapterChange(chapterId, oldChapter, newChapter));
            }
        }

        return changes.ToImmutable();
    }

    private static bool TryCreateValueOnlyChanges(
        EditableChapterDocument before,
        EditableChapterDocument after,
        out ImmutableArray<TrackChange> changes)
    {
        var result = ImmutableArray.CreateBuilder<TrackChange>();
        if (before.Tracks.Length != after.Tracks.Length)
        {
            changes = [];
            return false;
        }

        for (var trackIndex = 0; trackIndex < before.Tracks.Length; trackIndex++)
        {
            var oldTrack = before.Tracks[trackIndex];
            var newTrack = after.Tracks[trackIndex];
            if (oldTrack.Id != newTrack.Id || oldTrack.Chapters.Length != newTrack.Chapters.Length)
            {
                changes = [];
                return false;
            }

            var chapterChanges = ImmutableArray.CreateBuilder<ChapterChange>();
            for (var chapterIndex = 0; chapterIndex < oldTrack.Chapters.Length; chapterIndex++)
            {
                var oldChapter = oldTrack.Chapters[chapterIndex];
                var newChapter = newTrack.Chapters[chapterIndex];
                if (oldChapter.Id != newChapter.Id)
                {
                    changes = [];
                    return false;
                }

                if (oldChapter != newChapter)
                {
                    chapterChanges.Add(new ChapterChange(oldChapter.Id, oldChapter, newChapter));
                }
            }

            if (!string.Equals(oldTrack.Name, newTrack.Name, StringComparison.Ordinal)
                || oldTrack.FrameRate != newTrack.FrameRate
                || !oldTrack.Segments.SequenceEqual(newTrack.Segments)
                || chapterChanges.Count != 0)
            {
                result.Add(new TrackChange(
                    oldTrack.Id,
                    null,
                    null,
                    oldTrack.Name,
                    newTrack.Name,
                    chapterChanges.ToImmutable(),
                    null,
                    null,
                    oldTrack.Segments,
                    newTrack.Segments,
                    oldTrack.FrameRate,
                    newTrack.FrameRate));
            }
        }

        changes = result.ToImmutable();
        return true;
    }

    public EditableChapterDocument Apply(EditableChapterDocument document, bool forward)
    {
        var values = forward ? AfterValues : BeforeValues;
        var order = (forward ? AfterTrackOrder : BeforeTrackOrder) ?? document.Tracks.Select(track => track.Id).ToImmutableArray();
        var changes = TrackChanges.ToDictionary(change => change.Id);
        var tracks = document.Tracks.ToDictionary(track => track.Id);
        var output = ApplyTrackOrder(order, changes, tracks, forward);

        return new EditableChapterDocument(
            values.Id,
            values.Title,
            values.SourceName,
            values.ImportFormat,
            values.Duration,
            output.MoveToImmutable());
    }

    private static ImmutableArray<EditableChapterTrack>.Builder ApplyTrackOrder(
        ImmutableArray<ChapterTrackId> order,
        IReadOnlyDictionary<ChapterTrackId, TrackChange> changes,
        IReadOnlyDictionary<ChapterTrackId, EditableChapterTrack> tracks,
        bool forward)
    {
        var output = ImmutableArray.CreateBuilder<EditableChapterTrack>(order.Length);
        foreach (var trackId in order)
        {
            var track = ResolveTrack(trackId, changes, tracks, forward);
            if (track is not null)
            {
                output.Add(track);
            }
        }

        return output;
    }

    private static EditableChapterTrack? ResolveTrack(
        ChapterTrackId trackId,
        IReadOnlyDictionary<ChapterTrackId, TrackChange> changes,
        IReadOnlyDictionary<ChapterTrackId, EditableChapterTrack> tracks,
        bool forward)
    {
        if (!changes.TryGetValue(trackId, out var change))
        {
            return tracks.GetValueOrDefault(trackId);
        }

        var replacement = forward ? change.AfterTrack : change.BeforeTrack;
        if (replacement is not null || change.BeforeTrack is not null || change.AfterTrack is not null)
        {
            return replacement;
        }

        return tracks.TryGetValue(trackId, out var currentTrack) ? change.Apply(currentTrack, forward) : null;
    }
}

internal sealed record TrackChange(
    ChapterTrackId Id,
    EditableChapterTrack? BeforeTrack,
    EditableChapterTrack? AfterTrack,
    string? BeforeName,
    string? AfterName,
    ImmutableArray<ChapterChange> ChapterChanges,
    ImmutableArray<ChapterId>? BeforeChapterOrder,
    ImmutableArray<ChapterId>? AfterChapterOrder,
    ImmutableArray<EditableChapterSegment>? BeforeSegments,
    ImmutableArray<EditableChapterSegment>? AfterSegments,
    FrameRateValue BeforeRate = default,
    FrameRateValue AfterRate = default)
{
    public EditableChapterTrack Apply(EditableChapterTrack track, bool forward)
    {
        var order = (forward ? AfterChapterOrder : BeforeChapterOrder) ?? track.Chapters.Select(chapter => chapter.Id).ToImmutableArray();
        var changes = ChapterChanges.ToDictionary(change => change.Id);
        var chapters = track.Chapters.ToDictionary(chapter => chapter.Id);
        var output = ImmutableArray.CreateBuilder<EditableChapter>(order.Length);
        foreach (var chapterId in order)
        {
            if (changes.TryGetValue(chapterId, out var change))
            {
                var replacement = forward ? change.After : change.Before;
                if (replacement is not null)
                {
                    output.Add(replacement);
                }

                continue;
            }

            if (chapters.TryGetValue(chapterId, out var chapter))
            {
                output.Add(chapter);
            }
        }

        return new EditableChapterTrack(
            Id,
            forward ? AfterName ?? track.Name : BeforeName ?? track.Name,
            output.MoveToImmutable(),
            (forward ? AfterSegments : BeforeSegments) ?? track.Segments,
            forward ? AfterRate : BeforeRate);
    }
}

internal sealed record ChapterChange(ChapterId Id, EditableChapter? Before, EditableChapter? After);

internal sealed record DocumentValues(
    ChapterDocumentId Id,
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    ChapterDuration Duration,
    FrameRateValue FrameRate)
{
    public static DocumentValues From(EditableChapterDocument document) =>
        new(document.Id, document.Title, document.SourceName, document.ImportFormat, document.Duration, document.FrameRate);
}

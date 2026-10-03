using System.Collections.Immutable;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>Describes one node in a session's retained edit history.</summary>
public sealed record SessionHistoryNodeSnapshot(
    Guid Id,
    Guid? ParentId,
    ImmutableArray<Guid> ChildIds,
    Guid? PreferredChildId,
    string Description);

/// <summary>A stable view of the complete retained history tree.</summary>
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
    ResourceFailure
}

/// <summary>The result of a history navigation command.</summary>
public sealed record HistoryNavigationOutcome(
    HistoryNavigationOutcomeKind Kind,
    SessionSnapshot Snapshot,
    Guid CursorId,
    ImmutableArray<string> Errors);

internal enum SessionHistoryFailurePoint
{
    ChangeSetConstruction,
    NodePublication,
    Reconstruction
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
        var root = new SessionHistoryNode(rootId, null, [], null, "Document loaded", null);
        return new SessionHistoryTree(document, rootId, rootId, ImmutableDictionary<Guid, SessionHistoryNode>.Empty.Add(rootId, root));
    }

    public SessionHistoryTree Append(Guid id, DomainChangeSet changeSet, string description)
    {
        var parent = Nodes[CursorId];
        var updatedParent = parent with
        {
            Children = parent.Children.Add(id),
            PreferredChildId = id
        };
        var node = new SessionHistoryNode(id, CursorId, [], null, description, changeSet);
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
            result.Add(new SessionHistoryNodeSnapshot(node.Id, node.ParentId, node.Children, node.PreferredChildId, node.Description));
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
    DomainChangeSet? ChangeSet);

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
            if (oldTrack is null || newTrack is null)
            {
                changes.Add(new TrackChange(trackId, oldTrack, newTrack, null, null, [], null, null));
                continue;
            }

            var valueChanges = ImmutableArray.CreateBuilder<ChapterChange>();
            var oldChapters = oldTrack.Chapters.ToDictionary(chapter => chapter.Id);
            var newChapters = newTrack.Chapters.ToDictionary(chapter => chapter.Id);
            foreach (var chapterId in oldChapters.Keys.Union(newChapters.Keys))
            {
                oldChapters.TryGetValue(chapterId, out var oldChapter);
                newChapters.TryGetValue(chapterId, out var newChapter);
                if (oldChapter != newChapter)
                {
                    valueChanges.Add(new ChapterChange(chapterId, oldChapter, newChapter));
                }
            }

            var oldOrder = oldTrack.Chapters.Select(chapter => chapter.Id).ToImmutableArray();
            var newOrder = newTrack.Chapters.Select(chapter => chapter.Id).ToImmutableArray();
            var orderChanged = !oldOrder.AsSpan().SequenceEqual(newOrder.AsSpan());
            if (!string.Equals(oldTrack.Name, newTrack.Name, StringComparison.Ordinal)
                || valueChanges.Count != 0
                || orderChanged)
            {
                changes.Add(new TrackChange(
                    trackId,
                    null,
                    null,
                    oldTrack.Name,
                    newTrack.Name,
                    valueChanges.ToImmutable(),
                    orderChanged ? oldOrder : null,
                    orderChanged ? newOrder : null));
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

            if (!string.Equals(oldTrack.Name, newTrack.Name, StringComparison.Ordinal) || chapterChanges.Count != 0)
            {
                result.Add(new TrackChange(
                    oldTrack.Id,
                    null,
                    null,
                    oldTrack.Name,
                    newTrack.Name,
                    chapterChanges.ToImmutable(),
                    null,
                    null));
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
        var output = ImmutableArray.CreateBuilder<EditableChapterTrack>(order.Length);
        foreach (var trackId in order)
        {
            if (changes.TryGetValue(trackId, out var change))
            {
                var replacement = forward ? change.AfterTrack : change.BeforeTrack;
                if (replacement is not null)
                {
                    output.Add(replacement);
                    continue;
                }

                if (change.BeforeTrack is not null || change.AfterTrack is not null)
                {
                    continue;
                }

                if (tracks.TryGetValue(trackId, out var currentTrack))
                {
                    output.Add(change.Apply(currentTrack, forward));
                }

                continue;
            }

            if (tracks.TryGetValue(trackId, out var unchangedTrack))
            {
                output.Add(unchangedTrack);
            }
        }

        return new EditableChapterDocument(
            values.Id,
            values.Title,
            values.SourceName,
            values.ImportFormat,
            values.Duration,
            values.FrameRate,
            output.MoveToImmutable());
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
    ImmutableArray<ChapterId>? AfterChapterOrder)
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

        return new EditableChapterTrack(Id, forward ? AfterName ?? track.Name : BeforeName ?? track.Name, output.MoveToImmutable());
    }
}

internal sealed record ChapterChange(ChapterId Id, EditableChapter? Before, EditableChapter? After);

internal sealed record DocumentValues(
    ChapterDocumentId Id,
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    ChapterDuration Duration,
    ChapterFrameRate? FrameRate)
{
    public static DocumentValues From(EditableChapterDocument document) =>
        new(document.Id, document.Title, document.SourceName, document.ImportFormat, document.Duration, document.FrameRate);
}

using System.Collections.Immutable;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

internal static class HistoryNodeDetailsProjector
{
    public static ImmutableArray<HistoryFieldChange> ProjectChanges(
        EditableChapterDocument before,
        EditableChapterDocument after,
        CancellationToken cancellationToken)
    {
        var oldValues = Flatten(before, cancellationToken);
        var newValues = Flatten(after, cancellationToken);
        var changes = ImmutableArray.CreateBuilder<HistoryFieldChange>();
        foreach (var key in oldValues.Keys.Union(newValues.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasBefore = oldValues.TryGetValue(key, out var oldValue);
            var hasAfter = newValues.TryGetValue(key, out var newValue);
            if (hasBefore && hasAfter && ValuesEqual(oldValue!.Value, newValue!.Value))
            {
                continue;
            }

            var template = hasAfter ? newValue! : oldValue!;
            changes.Add(new HistoryFieldChange(
                template.Group,
                template.Id,
                template.Field,
                new HistoryFieldValue(hasBefore, oldValue?.Value),
                new HistoryFieldValue(hasAfter, newValue?.Value),
                template.Label,
                template.Context,
                before.FrameRate,
                after.FrameRate));
        }

        return changes.ToImmutable();
    }

    private static Dictionary<string, FieldValue> Flatten(EditableChapterDocument document, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, FieldValue>(StringComparer.Ordinal);
        var documentId = document.Id.Value.ToString("D");
        Add("Document", documentId, "Title", document.Title, "Document");
        Add("Document", documentId, "SourceName", document.SourceName, "Document");
        Add("Document", documentId, "ImportFormat", document.ImportFormat, "Document");
        Add("Document", documentId, "Duration", document.Duration, "Document");
        Add("Document", documentId, "FrameRate", document.FrameRate, "Document");
        Add("Document", documentId, "TrackOrder", document.Tracks.Select((track, index) => new HistoryOrderItem(track.Id.ToString(), $"{index + 1}. {track.Name}")).ToImmutableArray(), "Document");
        var tracksById = document.Tracks.ToDictionary(static track => track.Id, static track => track);

        foreach (var track in document.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trackId = track.Id.Value.ToString("D");
            var trackLabel = track.Name;
            Add("Track", trackId, "Presence", true, trackLabel);
            Add("Track", trackId, "Name", track.Name, trackLabel);
            Add("Track", trackId, "ChapterOrder", track.Chapters.Select((chapter, index) => new HistoryOrderItem(chapter.Id.ToString(), $"{index + 1}. {chapter.DisplayNumber} — {chapter.Name}")).ToImmutableArray(), trackLabel);
            Add("Track", trackId, "SegmentOrder", track.Segments.Select((segment, index) => new HistoryOrderItem(segment.Id.ToString(), $"{index + 1}. {segment.Name}")).ToImmutableArray(), trackLabel);

            for (var chapterIndex = 0; chapterIndex < track.Chapters.Length; chapterIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chapter = track.Chapters[chapterIndex];
                var chapterId = chapter.Id.Value.ToString("D");
                var chapterLabel = (chapterIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var context = trackLabel;
                Add("Chapter", chapterId, "Presence", true, chapterLabel, context);
                Add("Chapter", chapterId, "Track", new HistoryTrackReference(track.Id, trackLabel), chapterLabel, context);
                Add("Chapter", chapterId, "Position", chapterIndex + 1, chapterLabel, context);
                Add("Chapter", chapterId, "DisplayNumber", chapter.DisplayNumber, chapterLabel, context);
                Add("Chapter", chapterId, "Name", chapter.Name, chapterLabel, context);
                Add("Chapter", chapterId, "StartTicks", chapter.StartTicks, chapterLabel, context);
                Add("Chapter", chapterId, "EndTicks", chapter.EndTicks, chapterLabel, context);
                Add("Chapter", chapterId, "FramesInfo", chapter.FramesInfo, chapterLabel, context);
                Add("Chapter", chapterId, "FrameAccuracy", chapter.FrameAccuracy, chapterLabel, context);
                Add("Chapter", chapterId, "Kind", chapter.Kind, chapterLabel, context);
            }

            for (var segmentIndex = 0; segmentIndex < track.Segments.Length; segmentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = track.Segments[segmentIndex];
                var segmentId = segment.Id.ToString("D");
                var segmentLabel = segment.Name;
                Add("Segment", segmentId, "Presence", true, segmentLabel, trackLabel);
                Add("Segment", segmentId, "Track", new HistoryTrackReference(track.Id, trackLabel), segmentLabel, trackLabel);
                var sourceTrackLabel = tracksById.TryGetValue(segment.SourceTrackId, out var sourceTrack)
                    ? sourceTrack.Name
                    : string.Empty;
                Add("Segment", segmentId, "SourceTrackId", new HistoryTrackReference(segment.SourceTrackId, sourceTrackLabel), segmentLabel, trackLabel);
                Add("Segment", segmentId, "SourceEntryId", segment.SourceEntryId, segmentLabel, trackLabel);
                Add("Segment", segmentId, "Name", segment.Name, segmentLabel, trackLabel);
                Add("Segment", segmentId, "Position", segmentIndex + 1, segmentLabel, trackLabel);
                Add("Segment", segmentId, "StartTicks", segment.StartTicks, segmentLabel, trackLabel);
                Add("Segment", segmentId, "Duration", segment.Duration, segmentLabel, trackLabel);
                Add("Segment", segmentId, "FrameRate", segment.FrameRate, segmentLabel, trackLabel);
                Add("Segment", segmentId, "ImportFormat", segment.ImportFormat, segmentLabel, trackLabel);
                Add("Segment", segmentId, "SourceName", segment.SourceName, segmentLabel, trackLabel);
                Add("Segment", segmentId, "ReferencedMediaFiles", segment.ReferencedMediaFiles, segmentLabel, trackLabel);
                Add("Segment", segmentId, "MediaTracks", segment.MediaTracks, segmentLabel, trackLabel);
                Add("Segment", segmentId, "ChapterCount", segment.ChapterCount, segmentLabel, trackLabel);
                Add("Segment", segmentId, "ImportDisplayName", segment.ImportDisplayName, segmentLabel, trackLabel);
                Add("Segment", segmentId, "DiscTitleNumber", segment.DiscTitleNumber, segmentLabel, trackLabel);
                Add("Segment", segmentId, "SourceFrameRate", segment.SourceFrameRate, segmentLabel, trackLabel);
                Add("Segment", segmentId, "SourceChapterCount", segment.SourceChapterCount, segmentLabel, trackLabel);
            }
        }

        return values;

        void Add(string group, string id, string field, object? value, string label, string? context = null)
        {
            values[$"{group}|{id}|{field}"] = new FieldValue(group, id, field, value, label, context);
        }
    }

    private sealed record FieldValue(string Group, string Id, string Field, object? Value, string Label, string? Context);

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is HistoryTrackReference leftTrack && right is HistoryTrackReference rightTrack)
        {
            return leftTrack.Id == rightTrack.Id;
        }
        if (left is HistoryOrderItem leftOrder && right is HistoryOrderItem rightOrder)
        {
            return string.Equals(leftOrder.Id, rightOrder.Id, StringComparison.Ordinal);
        }
        if (ReferenceEquals(left, right) || Equals(left, right))
        {
            return true;
        }
        if (left is not System.Collections.IEnumerable leftValues || right is not System.Collections.IEnumerable rightValues)
        {
            return false;
        }
        var leftEnumerator = leftValues.GetEnumerator();
        var rightEnumerator = rightValues.GetEnumerator();
        while (true)
        {
            var hasLeft = leftEnumerator.MoveNext();
            var hasRight = rightEnumerator.MoveNext();
            if (hasLeft != hasRight)
            {
                return false;
            }
            if (!hasLeft)
            {
                return true;
            }
            if (!ValuesEqual(leftEnumerator.Current, rightEnumerator.Current))
            {
                return false;
            }
        }
    }
}

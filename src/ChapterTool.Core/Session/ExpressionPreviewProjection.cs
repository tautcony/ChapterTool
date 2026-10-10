using System.Collections.Immutable;
using System.Globalization;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

#pragma warning disable CS1591

namespace ChapterTool.Core.Session;

[Flags]
public enum ExpressionChapterChangeKind
{
    None = 0,
    Time = 1,
    FrameInformation = 2,
    OtherProperties = 4
}

public enum ExpressionPropertyOwnerKind
{
    Document,
    Track,
    Segment,
    Chapter
}

public sealed record ExpressionFrameValue(
    string Text,
    long? FrameCount,
    ChapterFrameRate? FrameRate,
    FrameAccuracy Accuracy)
{
    public bool IsMissing => string.IsNullOrWhiteSpace(Text);
}

public sealed record ExpressionChapterComparison(
    ChapterId Id,
    ChapterTrackId TrackId,
    string TrackName,
    EditableChapter? Before,
    EditableChapter? Candidate,
    ExpressionFrameValue? BeforeFrames,
    ExpressionFrameValue? CandidateFrames,
    ExpressionChapterChangeKind Changes)
{
    public long? DeltaTicks => Before is not null && Candidate is not null
        ? Candidate.StartTicks - Before.StartTicks
        : null;

    public long? DeltaFrames => BeforeFrames?.FrameCount is { } before
        && CandidateFrames?.FrameCount is { } candidate
        && BeforeFrames.FrameRate is { } beforeRate
        && CandidateFrames.FrameRate == beforeRate
            ? candidate - before
            : null;
}

public sealed record ExpressionPropertyComparison(
    ExpressionPropertyOwnerKind OwnerKind,
    Guid? OwnerId,
    string OwnerName,
    string Property,
    object? Before,
    object? Candidate);

public sealed record ExpressionPreviewProjection(
    ChapterTrackId? ScopeTrackId,
    string ScopeTrackName,
    int ParticipatingChapterCount,
    int ExcludedSeparatorCount,
    ImmutableArray<ExpressionChapterComparison> Chapters,
    ImmutableArray<ExpressionPropertyComparison> Properties,
    int AffectedChapterCount,
    int TimeChangedChapterCount,
    int FrameInformationChangedChapterCount,
    bool HasChanges)
{
    public bool TimesUnchanged => TimeChangedChapterCount == 0;
}

/// <summary>Builds host-neutral expression review data from immutable session snapshots.</summary>
public static class ExpressionPreviewProjector
{
    public static ExpressionPreviewProjection Build(
        ChapterContentPreview preview,
        ChapterTrackId? scopeTrackId = null,
        decimal? displayFrameRate = null,
        int frameDecimalPlaces = -1)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var targetIds = preview.TargetIds;
        scopeTrackId ??= FindScopeTrack(preview);
        var scopedBefore = SelectTracks(preview.Before, scopeTrackId);
        var scopedCandidate = SelectTracks(preview.Candidate, scopeTrackId);
        var scopeName = scopedCandidate.FirstOrDefault()?.Name
            ?? scopedBefore.FirstOrDefault()?.Name
            ?? string.Empty;

        var beforeChapters = scopedBefore.SelectMany(static track => track.Chapters)
            .Where(chapter => targetIds.Contains(chapter.Id) && chapter.Kind != ChapterKind.Separator)
            .ToDictionary(static chapter => chapter.Id);
        var candidateChapters = scopedCandidate.SelectMany(static track => track.Chapters)
            .Where(chapter => targetIds.Contains(chapter.Id) && chapter.Kind != ChapterKind.Separator)
            .ToDictionary(static chapter => chapter.Id);
        var orderedIds = scopedCandidate.SelectMany(static track => track.Chapters)
            .Where(chapter => targetIds.Contains(chapter.Id) && chapter.Kind != ChapterKind.Separator)
            .Select(static chapter => chapter.Id)
            .Concat(scopedBefore.SelectMany(static track => track.Chapters)
                .Where(chapter => targetIds.Contains(chapter.Id) && chapter.Kind != ChapterKind.Separator)
                .Select(static chapter => chapter.Id))
            .Distinct()
            .ToArray();

        var comparisons = ImmutableArray.CreateBuilder<ExpressionChapterComparison>(orderedIds.Length);
        var properties = ImmutableArray.CreateBuilder<ExpressionPropertyComparison>();
        foreach (var chapterId in orderedIds)
        {
            beforeChapters.TryGetValue(chapterId, out var before);
            candidateChapters.TryGetValue(chapterId, out var candidate);
            var beforeTrack = scopedBefore.FirstOrDefault(track => track.Chapters.Any(item => item.Id == chapterId));
            var candidateTrack = scopedCandidate.FirstOrDefault(track => track.Chapters.Any(item => item.Id == chapterId));
            var track = candidateTrack ?? beforeTrack;
            if (track is null)
            {
                continue;
            }

            var changes = Classify(before, candidate);
            if ((changes & ExpressionChapterChangeKind.OtherProperties) != 0)
            {
                AddChapterProperties(properties, beforeTrack, candidateTrack, before, candidate);
            }

            comparisons.Add(new ExpressionChapterComparison(
                chapterId,
                track.Id,
                track.Name,
                before,
                candidate,
                ToFrameValue(before, beforeTrack, preview.Before.FrameRate, displayFrameRate, frameDecimalPlaces),
                ToFrameValue(candidate, candidateTrack, preview.Candidate.FrameRate, displayFrameRate, frameDecimalPlaces),
                changes));
        }

        AddDocumentProperties(properties, preview.Before, preview.Candidate);
        AddTrackProperties(properties, preview.Before, preview.Candidate);
        AddSegmentProperties(properties, preview.Before, preview.Candidate);

        var chapterResults = comparisons.ToImmutable();
        var affected = chapterResults.Count(item => item.Changes != ExpressionChapterChangeKind.None);
        var timeChanged = chapterResults.Count(item => (item.Changes & ExpressionChapterChangeKind.Time) != 0);
        var framesChanged = chapterResults.Count(item => (item.Changes & ExpressionChapterChangeKind.FrameInformation) != 0);
        var participatingCount = scopedCandidate.SelectMany(static track => track.Chapters)
            .Count(chapter => targetIds.Contains(chapter.Id) && chapter.Kind != ChapterKind.Separator);
        var excludedSeparators = scopedBefore.Concat(scopedCandidate)
            .SelectMany(static track => track.Chapters)
            .Where(chapter => targetIds.Contains(chapter.Id) && chapter.Kind == ChapterKind.Separator)
            .Select(static chapter => chapter.Id)
            .Distinct()
            .Count();

        return new ExpressionPreviewProjection(
            scopeTrackId,
            scopeName,
            participatingCount,
            excludedSeparators,
            chapterResults,
            properties.ToImmutable(),
            affected,
            timeChanged,
            framesChanged,
            !DocumentsEqual(preview.Before, preview.Candidate));
    }

    public static bool DocumentsEqual(EditableChapterDocument left, EditableChapterDocument right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Id == right.Id
            && left.Title == right.Title
            && left.SourceName == right.SourceName
            && left.ImportFormat == right.ImportFormat
            && left.Duration == right.Duration
            && left.FrameRate == right.FrameRate
            && left.Tracks.Length == right.Tracks.Length
            && left.Tracks.Zip(right.Tracks).All(static pair =>
                pair.First.Id == pair.Second.Id
                && pair.First.Name == pair.Second.Name
                && pair.First.Chapters.SequenceEqual(pair.Second.Chapters)
                && SegmentsEqual(pair.First.Segments, pair.Second.Segments));
    }

    private static ChapterTrackId? FindScopeTrack(ChapterContentPreview preview)
    {
        foreach (var track in preview.Before.Tracks.Concat(preview.Candidate.Tracks))
        {
            if (track.Chapters.Any(chapter => preview.TargetIds.Contains(chapter.Id)))
            {
                return track.Id;
            }
        }

        if (preview.Before.Tracks.Length == 1)
        {
            return preview.Before.Tracks[0].Id;
        }

        return preview.Candidate.Tracks.Length == 1
            ? preview.Candidate.Tracks[0].Id
            : null;
    }

    private static ImmutableArray<EditableChapterTrack> SelectTracks(
        EditableChapterDocument document,
        ChapterTrackId? scopeTrackId) => scopeTrackId is { } id
        ? document.Tracks.Where(track => track.Id == id).ToImmutableArray()
        : document.Tracks;

    private static ExpressionChapterChangeKind Classify(EditableChapter? before, EditableChapter? candidate)
    {
        if (before is null || candidate is null)
        {
            return ExpressionChapterChangeKind.OtherProperties;
        }

        var result = ExpressionChapterChangeKind.None;
        if (before.StartTicks != candidate.StartTicks || before.EndTicks != candidate.EndTicks)
        {
            result |= ExpressionChapterChangeKind.Time;
        }

        if (before.FramesInfo != candidate.FramesInfo || before.FrameAccuracy != candidate.FrameAccuracy)
        {
            result |= ExpressionChapterChangeKind.FrameInformation;
        }

        if (before.DisplayNumber != candidate.DisplayNumber
            || before.Name != candidate.Name
            || before.Kind != candidate.Kind)
        {
            result |= ExpressionChapterChangeKind.OtherProperties;
        }

        return result;
    }

    private static ExpressionFrameValue? ToFrameValue(
        EditableChapter? chapter,
        EditableChapterTrack? track,
        ChapterFrameRate? documentRate,
        decimal? displayFrameRate,
        int frameDecimalPlaces)
    {
        if (chapter is null)
        {
            return null;
        }

        var rate = track?.Segments.FirstOrDefault()?.FrameRate ?? documentRate;
        if (displayFrameRate is > 0)
        {
            var frames = (decimal)chapter.StartTicks / TimeSpan.TicksPerSecond * displayFrameRate.Value;
            var text = frameDecimalPlaces == 0
                ? ChapterRounding.RoundToInt64(frames).ToString(CultureInfo.InvariantCulture)
                : frameDecimalPlaces > 0
                    ? frames.ToString($"F{Math.Clamp(frameDecimalPlaces, 1, 6)}", CultureInfo.InvariantCulture)
                    : frames.ToString(CultureInfo.InvariantCulture);
            var formattedAccuracy = frameDecimalPlaces == 0 ? chapter.FrameAccuracy : FrameAccuracy.Neutral;
            long? formattedCount = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
            return new ExpressionFrameValue(text, formattedCount, rate, formattedAccuracy);
        }

        long? frameCount = long.TryParse(chapter.FramesInfo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFrames)
            ? parsedFrames
            : null;
        return new ExpressionFrameValue(chapter.FramesInfo, frameCount, rate, chapter.FrameAccuracy);
    }

    private static void AddChapterProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        EditableChapterTrack? beforeTrack,
        EditableChapterTrack? candidateTrack,
        EditableChapter? before,
        EditableChapter? candidate)
    {
        var track = candidateTrack ?? beforeTrack;
        var owner = track?.Name ?? string.Empty;
        if (before is null || candidate is null)
        {
            Add(output, ExpressionPropertyOwnerKind.Chapter, before?.Id.Value ?? candidate?.Id.Value,
                $"{owner} · {candidate?.Name ?? before?.Name ?? string.Empty}", "Presence", before is not null, candidate is not null);
            return;
        }

        AddIfChanged(output, ExpressionPropertyOwnerKind.Chapter, before.Id.Value, owner, "DisplayNumber", before.DisplayNumber, candidate.DisplayNumber);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Chapter, before.Id.Value, owner, "Name", before.Name, candidate.Name);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Chapter, before.Id.Value, owner, "Kind", before.Kind, candidate.Kind);
    }

    private static void AddDocumentProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        EditableChapterDocument before,
        EditableChapterDocument candidate)
    {
        AddIfChanged(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "Title", before.Title, candidate.Title);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "SourceName", before.SourceName, candidate.SourceName);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "ImportFormat", before.ImportFormat, candidate.ImportFormat);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "Duration", before.Duration, candidate.Duration);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "FrameRate", before.FrameRate, candidate.FrameRate);
    }

    private static void AddTrackProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        EditableChapterDocument before,
        EditableChapterDocument candidate)
    {
        if (!before.Tracks.Select(static track => track.Id).SequenceEqual(candidate.Tracks.Select(static track => track.Id)))
        {
            Add(output, ExpressionPropertyOwnerKind.Document, before.Id.Value, before.Title, "TrackOrder",
                before.Tracks.Select(static track => track.Id).ToArray(), candidate.Tracks.Select(static track => track.Id).ToArray());
        }

        var oldTracks = before.Tracks.ToDictionary(static track => track.Id);
        var newTracks = candidate.Tracks.ToDictionary(static track => track.Id);
        foreach (var id in oldTracks.Keys.Union(newTracks.Keys))
        {
            oldTracks.TryGetValue(id, out var oldTrack);
            newTracks.TryGetValue(id, out var newTrack);
            var name = newTrack?.Name ?? oldTrack?.Name ?? string.Empty;
            if (oldTrack is null || newTrack is null)
            {
                Add(output, ExpressionPropertyOwnerKind.Track, id.Value, name, "Presence", oldTrack is not null, newTrack is not null);
                continue;
            }

            AddIfChanged(output, ExpressionPropertyOwnerKind.Track, id.Value, name, "Name", oldTrack.Name, newTrack.Name);
        }
    }

    private static void AddSegmentProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        EditableChapterDocument before,
        EditableChapterDocument candidate)
    {
        var oldSegments = before.Tracks.SelectMany(track => track.Segments.Select(segment => (track, segment)))
            .ToDictionary(static item => item.segment.Id);
        var newSegments = candidate.Tracks.SelectMany(track => track.Segments.Select(segment => (track, segment)))
            .ToDictionary(static item => item.segment.Id);
        foreach (var id in oldSegments.Keys.Union(newSegments.Keys))
        {
            oldSegments.TryGetValue(id, out var oldItem);
            newSegments.TryGetValue(id, out var newItem);
            var oldValue = oldItem.segment;
            var newValue = newItem.segment;
            var ownerTrackName = newItem.track?.Name ?? oldItem.track?.Name ?? string.Empty;
            var owner = newValue?.Name is { Length: > 0 } segmentName
                ? $"{ownerTrackName} · {segmentName}"
                : ownerTrackName;
            if (oldValue is null || newValue is null)
            {
                Add(output, ExpressionPropertyOwnerKind.Segment, id, owner, "Presence", oldValue is not null, newValue is not null);
                continue;
            }

            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "SourceTrackId", oldValue.SourceTrackId, newValue.SourceTrackId);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "SourceEntryId", oldValue.SourceEntryId, newValue.SourceEntryId);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "Name", oldValue.Name, newValue.Name);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "StartTicks", oldValue.StartTicks, newValue.StartTicks);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "Duration", oldValue.Duration, newValue.Duration);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "FrameRate", oldValue.FrameRate, newValue.FrameRate);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "ImportFormat", oldValue.ImportFormat, newValue.ImportFormat);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "SourceName", oldValue.SourceName, newValue.SourceName);
            if (!oldValue.ReferencedMediaFiles.SequenceEqual(newValue.ReferencedMediaFiles))
            {
                Add(output, ExpressionPropertyOwnerKind.Segment, id, owner, "ReferencedMediaFiles", oldValue.ReferencedMediaFiles, newValue.ReferencedMediaFiles);
            }

            if (!oldValue.MediaTracks.SequenceEqual(newValue.MediaTracks))
            {
                Add(output, ExpressionPropertyOwnerKind.Segment, id, owner, "MediaTracks", oldValue.MediaTracks, newValue.MediaTracks);
            }

            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "ChapterCount", oldValue.ChapterCount, newValue.ChapterCount);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "ImportDisplayName", oldValue.ImportDisplayName, newValue.ImportDisplayName);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "DiscTitleNumber", oldValue.DiscTitleNumber, newValue.DiscTitleNumber);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "SourceFrameRate", oldValue.SourceFrameRate, newValue.SourceFrameRate);
            AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, owner, "SourceChapterCount", oldValue.SourceChapterCount, newValue.SourceChapterCount);
        }
    }

    private static bool SegmentsEqual(
        ImmutableArray<EditableChapterSegment> left,
        ImmutableArray<EditableChapterSegment> right) =>
        left.Length == right.Length
        && left.Zip(right).All(static pair =>
            pair.First.Id == pair.Second.Id
            && pair.First.SourceTrackId == pair.Second.SourceTrackId
            && pair.First.SourceEntryId == pair.Second.SourceEntryId
            && pair.First.Name == pair.Second.Name
            && pair.First.StartTicks == pair.Second.StartTicks
            && pair.First.Duration == pair.Second.Duration
            && pair.First.FrameRate == pair.Second.FrameRate
            && pair.First.ImportFormat == pair.Second.ImportFormat
            && pair.First.SourceName == pair.Second.SourceName
            && pair.First.ReferencedMediaFiles.SequenceEqual(pair.Second.ReferencedMediaFiles)
            && pair.First.MediaTracks.SequenceEqual(pair.Second.MediaTracks)
            && pair.First.ChapterCount == pair.Second.ChapterCount
            && pair.First.ImportDisplayName == pair.Second.ImportDisplayName
            && pair.First.DiscTitleNumber == pair.Second.DiscTitleNumber
            && pair.First.SourceFrameRate == pair.Second.SourceFrameRate
            && pair.First.SourceChapterCount == pair.Second.SourceChapterCount);

    private static void AddIfChanged<T>(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        ExpressionPropertyOwnerKind ownerKind,
        Guid? ownerId,
        string ownerName,
        string property,
        T before,
        T candidate)
    {
        if (!EqualityComparer<T>.Default.Equals(before, candidate))
        {
            Add(output, ownerKind, ownerId, ownerName, property, before, candidate);
        }
    }

    private static void Add(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        ExpressionPropertyOwnerKind ownerKind,
        Guid? ownerId,
        string ownerName,
        string property,
        object? before,
        object? candidate) => output.Add(new ExpressionPropertyComparison(
            ownerKind,
            ownerId,
            ownerName,
            property,
            before,
            candidate));
}

#pragma warning restore CS1591

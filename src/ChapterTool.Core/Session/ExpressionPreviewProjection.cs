using System.Collections.Immutable;
using System.Globalization;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Session;

/// <summary>Flags the parts of a chapter changed by an expression candidate.</summary>
[Flags]
public enum ExpressionChapterChangeKind
{
    /// <summary>No chapter values changed.</summary>
    None = 0,
    /// <summary>The chapter start or end time changed.</summary>
    Time = 1,
    /// <summary>The stored frame information or its accuracy changed.</summary>
    FrameInformation = 2,
    /// <summary>A chapter property other than time or frame information changed.</summary>
    OtherProperties = 4
}

/// <summary>Identifies the document object that owns a changed property.</summary>
public enum ExpressionPropertyOwnerKind
{
    /// <summary>The document owns the property.</summary>
    Document,
    /// <summary>A track owns the property.</summary>
    Track,
    /// <summary>A source segment owns the property.</summary>
    Segment,
    /// <summary>A chapter owns the property.</summary>
    Chapter
}

/// <summary>Stores a chapter's frame display value and the rate used to interpret it.</summary>
/// <param name="Text">The displayed frame value.</param>
/// <param name="FrameCount">The integer frame count, when the text represents one.</param>
/// <param name="FrameRate">The rate associated with the frame value.</param>
/// <param name="Accuracy">The accuracy assigned to the frame value.</param>
/// <param name="SourceWasMissing">Whether the source chapter had no frame value.</param>
public sealed record ExpressionFrameValue(
    string Text,
    long? FrameCount,
    FrameRateValue FrameRate,
    FrameAccuracy Accuracy,
    bool SourceWasMissing = false)
{
    /// <summary>Gets whether the source frame value was missing or blank.</summary>
    public bool IsMissing => SourceWasMissing || string.IsNullOrWhiteSpace(Text);
}

/// <summary>Compares one targeted chapter before and after an expression.</summary>
/// <param name="Id">The chapter identity.</param>
/// <param name="TrackId">The containing track identity.</param>
/// <param name="TrackName">The containing track name.</param>
/// <param name="Before">The chapter before the expression, if present.</param>
/// <param name="Candidate">The chapter after the expression, if present.</param>
/// <param name="BeforeFrames">The prior frame display value, when available.</param>
/// <param name="CandidateFrames">The candidate frame display value, when available.</param>
/// <param name="Changes">The categories of values that changed.</param>
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
    /// <summary>Gets the candidate start-time change in timestamp ticks.</summary>
    public long? DeltaTicks => Before is not null && Candidate is not null
        ? Candidate.StartTicks - Before.StartTicks
        : null;

    /// <summary>Gets the frame-count change when both values use the same known rate.</summary>
    public long? DeltaFrames => BeforeFrames?.FrameCount is { } before
        && CandidateFrames?.FrameCount is { } candidate
        && BeforeFrames.FrameRate is { } beforeRate
        && beforeRate.IsKnown
        && CandidateFrames.FrameRate == beforeRate
            ? candidate - before
            : null;
}

/// <summary>Compares one property value before and after an expression.</summary>
/// <param name="OwnerKind">The kind of object that owns the property.</param>
/// <param name="OwnerId">The owner identity, when the owner has one.</param>
/// <param name="OwnerName">The owner label for display.</param>
/// <param name="Property">The property name.</param>
/// <param name="Before">The prior property value.</param>
/// <param name="Candidate">The candidate property value.</param>
public sealed record ExpressionPropertyComparison(
    ExpressionPropertyOwnerKind OwnerKind,
    Guid? OwnerId,
    string OwnerName,
    string Property,
    object? Before,
    object? Candidate);

/// <summary>Contains the host-neutral changes shown when reviewing an expression candidate.</summary>
/// <param name="ScopeTrackId">The selected track identity, if review is track-scoped.</param>
/// <param name="ScopeTrackName">The selected track name.</param>
/// <param name="ParticipatingChapterCount">The number of targeted non-separator chapters.</param>
/// <param name="ExcludedSeparatorCount">The number of targeted separators excluded from evaluation.</param>
/// <param name="Chapters">The before-and-candidate chapter comparisons.</param>
/// <param name="Properties">The before-and-candidate property comparisons.</param>
/// <param name="AffectedChapterCount">The number of chapters with any change.</param>
/// <param name="TimeChangedChapterCount">The number of chapters with changed times.</param>
/// <param name="FrameInformationChangedChapterCount">The number of chapters with changed frame information.</param>
/// <param name="HasChanges">Whether the candidate differs from the current document.</param>
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
    /// <summary>Gets whether no chapter start or end times changed.</summary>
    public bool TimesUnchanged => TimeChangedChapterCount == 0;
}

/// <summary>Builds host-neutral expression review data from immutable session snapshots.</summary>
public static class ExpressionPreviewProjector
{
    /// <summary>Builds the review projection for an expression candidate.</summary>
    /// <param name="preview">The current and candidate content snapshots.</param>
    /// <param name="scopeTrackId">An optional track scope for chapter comparisons.</param>
    /// <param name="displayFrameRate">An optional rate used to format frame values.</param>
    /// <param name="frameDecimalPlaces">The number of frame decimal places, or a negative value for raw precision.</param>
    /// <returns>The immutable review projection.</returns>
    public static ExpressionPreviewProjection Build(
        ChapterContentPreview preview,
        ChapterTrackId? scopeTrackId = null,
        FrameRateValue? displayFrameRate = null,
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

    /// <summary>Checks whether two documents have equal content and structure.</summary>
    /// <param name="left">The first document.</param>
    /// <param name="right">The second document.</param>
    /// <returns><see langword="true"/> when the documents are equal.</returns>
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
                && pair.First.FrameRate == pair.Second.FrameRate
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
        FrameRateValue documentRate,
        FrameRateValue? displayFrameRate,
        int frameDecimalPlaces)
    {
        if (chapter is null)
        {
            return null;
        }

        var rate = track?.FrameRate ?? documentRate;
        if (displayFrameRate is { IsKnown: true } displayRate)
        {
            var frames = ExactFrameMath.FromTicks(chapter.StartTicks, displayRate);
            var text = frameDecimalPlaces == 0
                ? frames.RoundAwayFromZero().ToString(CultureInfo.InvariantCulture)
                : frameDecimalPlaces > 0
                    ? frames.FormatFixed(Math.Clamp(frameDecimalPlaces, 1, 6))
                    : FormatRawFrames(frames);
            var formattedAccuracy = frameDecimalPlaces == 0 ? chapter.FrameAccuracy : FrameAccuracy.Neutral;
            long? formattedCount = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
            return new ExpressionFrameValue(text, formattedCount, rate, formattedAccuracy,
                string.IsNullOrWhiteSpace(chapter.FramesInfo));
        }

        long? frameCount = long.TryParse(chapter.FramesInfo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFrames)
            ? parsedFrames
            : null;
        return new ExpressionFrameValue(chapter.FramesInfo, frameCount, rate, chapter.FrameAccuracy,
            string.IsNullOrWhiteSpace(chapter.FramesInfo));
    }

    private static string FormatRawFrames(ExactFrameMath frames)
    {
        var text = frames.ToDecimal().ToString("G29", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal) ? text : text + ".0";
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
            AddIfChanged(output, ExpressionPropertyOwnerKind.Track, id.Value, name, "FrameRate", oldTrack.FrameRate, newTrack.FrameRate);
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
            AddSegmentDifference(output, id, oldItem.segment, newItem.segment,
                newItem.track?.Name ?? oldItem.track?.Name ?? string.Empty);
        }
    }

    private static void AddSegmentDifference(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        Guid id,
        EditableChapterSegment? before,
        EditableChapterSegment? candidate,
        string ownerTrackName)
    {
        var ownerName = candidate?.Name is { Length: > 0 } segmentName
            ? $"{ownerTrackName} · {segmentName}"
            : ownerTrackName;
        if (before is null || candidate is null)
        {
            Add(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "Presence", before is not null, candidate is not null);
            return;
        }

        AddSegmentCoreProperties(output, id, ownerName, before, candidate);
        AddSegmentMetadataProperties(output, id, ownerName, before, candidate);
    }

    private static void AddSegmentCoreProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        Guid id,
        string ownerName,
        EditableChapterSegment before,
        EditableChapterSegment candidate)
    {
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "SourceTrackId", before.SourceTrackId, candidate.SourceTrackId);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "SourceEntryId", before.SourceEntryId, candidate.SourceEntryId);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "Name", before.Name, candidate.Name);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "StartTicks", before.StartTicks, candidate.StartTicks);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "Duration", before.Duration, candidate.Duration);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "FrameRate", before.FrameRate, candidate.FrameRate);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "ImportFormat", before.ImportFormat, candidate.ImportFormat);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "SourceName", before.SourceName, candidate.SourceName);
    }

    private static void AddSegmentMetadataProperties(
        ImmutableArray<ExpressionPropertyComparison>.Builder output,
        Guid id,
        string ownerName,
        EditableChapterSegment before,
        EditableChapterSegment candidate)
    {
        if (!before.ReferencedMediaFiles.SequenceEqual(candidate.ReferencedMediaFiles))
        {
            Add(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "ReferencedMediaFiles", before.ReferencedMediaFiles, candidate.ReferencedMediaFiles);
        }

        if (!before.MediaTracks.SequenceEqual(candidate.MediaTracks))
        {
            Add(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "MediaTracks", before.MediaTracks, candidate.MediaTracks);
        }

        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "ChapterCount", before.ChapterCount, candidate.ChapterCount);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "ImportDisplayName", before.ImportDisplayName, candidate.ImportDisplayName);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "DiscTitleNumber", before.DiscTitleNumber, candidate.DiscTitleNumber);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "SourceFrameRate", before.SourceFrameRate, candidate.SourceFrameRate);
        AddIfChanged(output, ExpressionPropertyOwnerKind.Segment, id, ownerName, "SourceChapterCount", before.SourceChapterCount, candidate.SourceChapterCount);
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

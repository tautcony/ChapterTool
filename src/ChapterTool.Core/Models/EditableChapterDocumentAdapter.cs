using System.Collections.Immutable;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Models;

/// <summary>Converts between the immutable document model and the legacy ChapterSet contract.</summary>
public static class EditableChapterDocumentAdapter
{
    /// <summary>Gets the selected track's complete segment timeline duration.</summary>
    public static ChapterDuration TrackDuration(EditableChapterDocument source, int trackIndex)
    {
        var segments = source.Tracks[trackIndex].Segments;
        return segments.IsEmpty ? source.Duration : segments.Any(segment => !segment.Duration.IsKnown)
            ? ChapterDuration.Unknown
            : ChapterDuration.FromTicks(segments.Max(segment => checked(segment.StartTicks + segment.Duration.Ticks)));
    }

    /// <summary>Gets document duration after a selected track operation changes its timeline duration.</summary>
    public static ChapterDuration ReplacedTrackDuration(EditableChapterDocument source, int trackIndex, EditableChapterDocument focused)
    {
        if (focused.Duration == TrackDuration(source, trackIndex))
        {
            return source.Duration;
        }

        var durations = source.Tracks.Select((_, index) => index == trackIndex ? focused.Duration : TrackDuration(source, index)).ToArray();
        return durations.All(duration => duration.IsKnown)
            ? ChapterDuration.FromTicks(durations.Max(duration => duration.Ticks)) : ChapterDuration.Unknown;
    }

    /// <summary>Supplies a detected frame rate to a focused operation when source metadata has no rate.</summary>
    public static EditableChapterDocument WithFallbackFrameRate(EditableChapterDocument source, decimal framesPerSecond)
    {
        if (source.FrameRate is not null || framesPerSecond <= 0)
        {
            return source;
        }

        var rate = new ChapterFrameRate(checked((long)decimal.Round(framesPerSecond * 1_000_000m, 0, MidpointRounding.AwayFromZero)), 1_000_000);
        var tracks = source.Tracks.Select(track => new EditableChapterTrack(track.Id, track.Name, track.Chapters,
            track.Segments.Select(segment => segment with { FrameRate = segment.FrameRate ?? rate })));
        return new EditableChapterDocument(source.Id, source.Title, source.SourceName, source.ImportFormat,
            source.Duration, rate, tracks);
    }

    /// <summary>Creates one editable track for each imported source entry.</summary>
    public static EditableChapterDocument FromChapterImportSource(ChapterImportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Entries.Count == 0)
        {
            return new EditableChapterDocument(ChapterDocumentId.New(), string.Empty, source.SourcePath,
                ChapterImportFormat.Unknown, ChapterDuration.Unknown, null, []);
        }

        var tracks = ImmutableArray.CreateBuilder<EditableChapterTrack>(source.Entries.Count);
        var durationTicks = 0L;
        var effectiveRates = new HashSet<ChapterFrameRate>();
        foreach (var entry in source.Entries)
        {
            var document = FromChapterSet(entry.ChapterSet);
            var importedTrack = document.Tracks[0];
            var duration = document.Duration;
            if (duration.IsKnown)
            {
                durationTicks = checked(durationTicks + duration.Ticks);
            }

            if (document.FrameRate is ChapterFrameRate rate)
            {
                effectiveRates.Add(rate);
            }

            var segment = EditableChapterSegment.Create(
                importedTrack.Id,
                entry.Id,
                entry.DisplayName,
                0,
                duration,
                document.FrameRate,
                entry.ChapterSet.ImportFormat,
                entry.ChapterSet.SourceName ?? source.SourcePath,
                entry.ReferencedMediaFiles,
                entry.MediaTracks,
                entry.ChapterCount ?? entry.ChapterSet.Chapters.Count,
                entry.ImportDisplayName,
                entry.DiscTitleNumber,
                sourceChapterCount: entry.SourceChapterCount ?? entry.ChapterCount ?? entry.ChapterSet.Chapters.Count);
            tracks.Add(new EditableChapterTrack(importedTrack.Id, entry.DisplayName, importedTrack.Chapters, [segment]));
        }

        var first = source.Entries[0].ChapterSet;
        return new EditableChapterDocument(
            ChapterDocumentId.New(),
            first.Title,
            first.SourceName ?? source.SourcePath,
            first.ImportFormat,
            durationTicks > 0 ? ChapterDuration.FromTicks(durationTicks) : ChapterDuration.Unknown,
            effectiveRates.Count == 1 ? effectiveRates.Single() : null,
            tracks);
    }

    /// <summary>Projects one track in a multi-track document to the legacy chapter-set view.</summary>
    public static ChapterSet ToChapterSet(EditableChapterDocument document, int trackIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (trackIndex < 0 || trackIndex >= document.Tracks.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        }

        var track = document.Tracks[trackIndex];
        var segment = track.Segments.FirstOrDefault();
        var rate = segment?.FrameRate ?? document.FrameRate;
        var duration = TrackDuration(document, trackIndex);
        var singleTrackDocument = new EditableChapterDocument(
            document.Id,
            segment?.Name ?? track.Name,
            segment?.SourceName ?? document.SourceName,
            segment?.ImportFormat ?? document.ImportFormat,
            duration,
            rate,
            [track]);
        return ToChapterSet(singleTrackDocument);
    }

    /// <summary>Applies a legacy row edit to one selected track while preserving other tracks and identities.</summary>
    public static EditableChapterDocument ApplyTrackChapterSetResult(
        EditableChapterDocument source,
        int trackIndex,
        ChapterSet result)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (trackIndex < 0 || trackIndex >= source.Tracks.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        }

        var sourceTrack = source.Tracks[trackIndex];
        var frameRate = FromLegacyFrameRate(result.FramesPerSecond);
        var chapters = result.Chapters.Select((chapter, index) => new EditableChapter(
            index < sourceTrack.Chapters.Length ? sourceTrack.Chapters[index].Id : ChapterId.New(),
            chapter.DisplayNumber,
            chapter.StartTime.Ticks,
            chapter.Name,
            chapter.FramesInfo,
            chapter.EndTime?.Ticks,
            chapter.FrameAccuracy,
            chapter.Kind)).ToImmutableArray();
        var segments = sourceTrack.Segments
            .Select(segment => segment with { FrameRate = frameRate })
            .ToImmutableArray();
        var tracks = source.Tracks.SetItem(trackIndex,
            new EditableChapterTrack(sourceTrack.Id, sourceTrack.Name, chapters, segments));
        var effectiveRates = tracks
            .SelectMany(track => track.Segments.Select(segment => segment.FrameRate))
            .Where(static rate => rate.HasValue)
            .Select(static rate => rate!.Value)
            .Distinct()
            .ToArray();
        var duration = source.Duration.IsKnown ? source.Duration : result.Duration.Ticks > 0
            ? ChapterDuration.FromTicks(result.Duration.Ticks)
            : ChapterDuration.Unknown;
        return new EditableChapterDocument(source.Id, source.Title, source.SourceName, source.ImportFormat,
            duration, effectiveRates.Length == 1 ? effectiveRates[0] : effectiveRates.Length == 0 ? null : source.FrameRate, tracks);
    }

    /// <summary>Creates an immutable document snapshot from legacy chapter data.</summary>
    public static EditableChapterDocument FromChapterSet(ChapterSet chapterSet)
    {
        ArgumentNullException.ThrowIfNull(chapterSet);
        var chapters = chapterSet.Chapters.Select(chapter => new EditableChapter(
            ChapterId.New(),
            chapter.DisplayNumber,
            chapter.StartTime.Ticks,
            chapter.Name,
            chapter.FramesInfo,
            chapter.EndTime?.Ticks,
            chapter.FrameAccuracy,
            chapter.Kind));
        var duration = chapterSet.Duration.Ticks > 0
            ? ChapterDuration.FromTicks(chapterSet.Duration.Ticks)
            : ChapterDuration.Unknown;
        var frameRate = FromLegacyFrameRate(chapterSet.FramesPerSecond);
        return new EditableChapterDocument(
            ChapterDocumentId.New(),
            chapterSet.Title,
            chapterSet.SourceName,
            chapterSet.ImportFormat,
            duration,
            frameRate,
            [new EditableChapterTrack(ChapterTrackId.New(), chapterSet.Title, chapters)]);
    }

    /// <summary>Creates a legacy chapter set from an immutable document snapshot.</summary>
    public static ChapterSet ToChapterSet(EditableChapterDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var chapters = ImmutableArray.CreateBuilder<Chapter>();
        var hasPreviousTrack = false;
        foreach (var track in document.Tracks)
        {
            if (hasPreviousTrack && !track.Chapters.IsEmpty)
            {
                chapters.Add(Chapter.Separator(track.Name));
            }

            chapters.AddRange(track.Chapters.Select(chapter => new Chapter(
                chapter.DisplayNumber,
                TimeSpan.FromTicks(chapter.StartTicks),
                chapter.Name,
                chapter.FramesInfo,
                chapter.EndTicks is long endTicks ? TimeSpan.FromTicks(endTicks) : null,
                chapter.FrameAccuracy,
                chapter.Kind)));
            hasPreviousTrack = hasPreviousTrack || !track.Chapters.IsEmpty;
        }
        var framesPerSecond = document.FrameRate is ChapterFrameRate rate
            ? (double)rate.Numerator / rate.Denominator
            : 0d;
        var duration = document.Duration.IsKnown ? TimeSpan.FromTicks(document.Duration.Ticks) : TimeSpan.Zero;
        return new ChapterSet(document.Title, document.SourceName, document.ImportFormat, framesPerSecond, duration, chapters.ToImmutable());
    }

    /// <summary>
    /// Creates a document candidate from a non-structural legacy edit result.
    /// The source document and result must have one track and the same row count.
    /// Each row keeps the identity at its original position.
    /// </summary>
    public static EditableChapterDocument ApplyNonStructuralChapterSetResult(
        EditableChapterDocument source,
        ChapterSet result)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (source.Tracks.Length != 1)
        {
            throw new ArgumentException("A legacy ChapterSet edit requires a single-track source document.", nameof(source));
        }

        var sourceTrack = source.Tracks[0];
        if (sourceTrack.Chapters.Length != result.Chapters.Count)
        {
            throw new ArgumentException("A non-structural edit result must preserve the chapter row count.", nameof(result));
        }

        return ApplyLegacyChapterSetResult(source, result);
    }

    /// <summary>Applies an FPS conversion to chapter values and the current segment timeline.</summary>
    public static EditableChapterDocument ApplyFrameRateChangeResult(
        EditableChapterDocument source,
        ChapterSet result,
        decimal sourceFps,
        decimal targetFps)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (source.Tracks.Length != 1)
        {
            throw new ArgumentException("A legacy ChapterSet edit requires a single-track source document.", nameof(source));
        }

        var updated = ApplyLegacyChapterSetResult(source, result);
        var track = updated.Tracks[0];
        var sourceSegments = source.Tracks[0].Segments.ToDictionary(static segment => segment.Id);
        var segments = track.Segments.Select(segment => segment with
        {
            StartTicks = ChapterFpsTransformService.ConvertTicks(segment.StartTicks, sourceFps, targetFps),
            Duration = segment.Duration.IsKnown
                ? ChapterDuration.FromTicks(ChapterFpsTransformService.ConvertTicks(checked(segment.StartTicks + segment.Duration.Ticks), sourceFps, targetFps)
                    - ChapterFpsTransformService.ConvertTicks(segment.StartTicks, sourceFps, targetFps))
                : ChapterDuration.Unknown,
            FrameRate = FromLegacyFrameRate((double)targetFps),
            SourceFrameRate = sourceSegments.TryGetValue(segment.Id, out var original)
                ? original.SourceFrameRate ?? original.FrameRate
                : segment.SourceFrameRate ?? segment.FrameRate
        }).ToImmutableArray();
        return new EditableChapterDocument(updated.Id, updated.Title, updated.SourceName, updated.ImportFormat,
            updated.Duration, FromLegacyFrameRate((double)targetFps),
            [new EditableChapterTrack(track.Id, track.Name, track.Chapters, segments)]);
    }

    /// <summary>Applies a legacy ChapterSet result while preserving positional identities when possible.</summary>
    public static EditableChapterDocument ApplyLegacyChapterSetResult(
        EditableChapterDocument source,
        ChapterSet result)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (source.Tracks.Length != 1)
        {
            throw new ArgumentException("A legacy ChapterSet edit requires a single-track source document.", nameof(source));
        }

        var chapters = result.Chapters.Select((chapter, index) => new EditableChapter(
            index < source.Tracks[0].Chapters.Length
                ? source.Tracks[0].Chapters[index].Id
                : ChapterId.New(),
            chapter.DisplayNumber,
            chapter.StartTime.Ticks,
            chapter.Name,
            chapter.FramesInfo,
            chapter.EndTime?.Ticks,
            chapter.FrameAccuracy,
            chapter.Kind));
        var duration = result.Duration.Ticks > 0
            ? ChapterDuration.FromTicks(result.Duration.Ticks)
            : ChapterDuration.Unknown;
        var frameRate = FromLegacyFrameRate(result.FramesPerSecond);
        var segments = source.Tracks[0].Segments;
        return new EditableChapterDocument(
            source.Id,
            result.Title,
            result.SourceName,
            result.ImportFormat,
            duration,
            frameRate,
            [new EditableChapterTrack(source.Tracks[0].Id, result.Title, chapters, segments)]);
    }

    private static ChapterFrameRate? FromLegacyFrameRate(double framesPerSecond)
    {
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0)
        {
            return null;
        }

        const long denominator = 1_000_000;
        var scaled = framesPerSecond * denominator;
        if (!double.IsFinite(scaled) || scaled < 1 || scaled > long.MaxValue)
        {
            return null;
        }

        return new ChapterFrameRate((long)Math.Round(scaled, MidpointRounding.AwayFromZero), denominator);
    }
}

/// <summary>Reports validation errors for an editable chapter document.</summary>
public sealed record EditableChapterDocumentValidationResult(bool IsValid, ImmutableArray<string> Errors);

/// <summary>Validates editable document identities and time bounds.</summary>
public static class EditableChapterDocumentValidator
{
    /// <summary>Checks identities, chapter times, and known duration bounds.</summary>
    public static EditableChapterDocumentValidationResult Validate(EditableChapterDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = ImmutableArray.CreateBuilder<string>();
        if (document.Id.Value == Guid.Empty)
        {
            errors.Add("Document identity must not be empty.");
        }

        var trackIds = new HashSet<ChapterTrackId>();
        var chapterIds = new HashSet<ChapterId>();
        var segmentIds = new HashSet<Guid>();
        foreach (var track in document.Tracks)
        {
            if (track.Id.Value == Guid.Empty)
            {
                errors.Add("Track identity must not be empty.");
            }

            if (!trackIds.Add(track.Id))
            {
                errors.Add("Track identities must be unique.");
            }

            foreach (var segment in track.Segments)
            {
                if (segment.Id == Guid.Empty || !segmentIds.Add(segment.Id))
                {
                    errors.Add("Segment identities must be non-empty and unique.");
                }

                if (segment.SourceTrackId.Value == Guid.Empty)
                {
                    errors.Add("Segment source track identity must not be empty.");
                }

                if (segment.StartTicks < 0)
                {
                    errors.Add("Segment start time must not be negative.");
                }

                if (segment.Duration.IsKnown)
                {
                    try
                    {
                        var endTicks = checked(segment.StartTicks + segment.Duration.Ticks);
                        if (document.Duration.IsKnown && endTicks > document.Duration.Ticks)
                        {
                            errors.Add("Segment exceeds the known document duration.");
                        }
                    }
                    catch (OverflowException)
                    {
                        errors.Add("Segment end time exceeds the supported time range.");
                    }
                }
            }

            foreach (var chapter in track.Chapters)
            {
                if (chapter.Id.Value == Guid.Empty)
                {
                    errors.Add("Chapter identity must not be empty.");
                }

                if (!chapterIds.Add(chapter.Id))
                {
                    errors.Add("Chapter identities must be unique.");
                }

                if (chapter.StartTicks < 0)
                {
                    errors.Add("Chapter start time must not be negative.");
                }

                if (chapter.EndTicks is long end && end < chapter.StartTicks)
                {
                    errors.Add("Chapter end time must not precede its start time.");
                }

                if (document.Duration.IsKnown && chapter.EndTicks is long endTicks && endTicks > document.Duration.Ticks)
                {
                    errors.Add("Chapter end time exceeds the known document duration.");
                }
            }
        }

        return new EditableChapterDocumentValidationResult(errors.Count == 0, errors.ToImmutable());
    }
}

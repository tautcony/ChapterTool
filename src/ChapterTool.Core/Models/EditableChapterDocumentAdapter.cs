using System.Collections.Immutable;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Models;

/// <summary>Converts between immutable documents and the retained ChapterSet boundary.</summary>
public static class EditableChapterDocumentAdapter
{
    /// <summary>Gets the complete selected-track timeline duration.</summary>
    public static ChapterDuration TrackDuration(EditableChapterDocument source, int trackIndex)
    {
        var segments = source.Tracks[trackIndex].Segments;
        return segments.IsEmpty ? source.Duration : segments.Any(static segment => !segment.Duration.IsKnown)
            ? ChapterDuration.Unknown
            : ChapterDuration.FromTicks(segments.Max(static segment => checked(segment.StartTicks + segment.Duration.Ticks)));
    }

    /// <summary>Gets duration after a selected-track timeline change.</summary>
    public static ChapterDuration ReplacedTrackDuration(EditableChapterDocument source, int trackIndex, EditableChapterDocument focused)
    {
        if (focused.Duration == TrackDuration(source, trackIndex))
        {
            return source.Duration;
        }

        var durations = source.Tracks.Select((_, index) => index == trackIndex ? focused.Duration : TrackDuration(source, index)).ToArray();
        return durations.All(static duration => duration.IsKnown)
            ? ChapterDuration.FromTicks(durations.Max(static duration => duration.Ticks)) : ChapterDuration.Unknown;
    }

    /// <summary>Creates one track for each imported source entry.</summary>
    public static EditableChapterDocument FromChapterImportSource(ChapterImportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tracks = ImmutableArray.CreateBuilder<EditableChapterTrack>(source.Entries.Count);
        var durationTicks = 0L;
        foreach (var entry in source.Entries)
        {
            var imported = FromChapterSet(entry.ChapterSet);
            var track = imported.Tracks[0];
            if (imported.Duration.IsKnown)
            {
                durationTicks = checked(durationTicks + imported.Duration.Ticks);
            }

            var segment = EditableChapterSegment.Create(track.Id, entry.Id, entry.DisplayName, 0, imported.Duration,
                entry.ChapterSet.FrameRate, entry.ChapterSet.ImportFormat, entry.ChapterSet.SourceName ?? source.SourcePath,
                entry.ReferencedMediaFiles, entry.MediaTracks,
                entry.ChapterCount ?? entry.ChapterSet.Chapters.Count, entry.ImportDisplayName, entry.DiscTitleNumber,
                sourceChapterCount: entry.SourceChapterCount ?? entry.ChapterCount ?? entry.ChapterSet.Chapters.Count);
            var segments = entry.Segments.IsEmpty ? [segment] : entry.Segments.Select(item =>
                EditableChapterSegment.Create(ChapterTrackId.New(), $"{entry.Id}/{item.Id}", item.Name, item.StartTicks,
                    item.Duration, item.FrameRate, entry.ChapterSet.ImportFormat, item.Name,
                    item.ReferencedMediaFiles, entry.MediaTracks,
                    track.Chapters.Count(chapter => chapter.StartTicks >= item.StartTicks
                        && (!item.Duration.IsKnown || chapter.StartTicks < checked(item.StartTicks + item.Duration.Ticks))),
                    entry.ImportDisplayName, entry.DiscTitleNumber)).ToImmutableArray();
            tracks.Add(new EditableChapterTrack(track.Id, entry.DisplayName, track.Chapters, segments));
        }

        var first = source.Entries.FirstOrDefault()?.ChapterSet;
        return new EditableChapterDocument(ChapterDocumentId.New(), first?.Title ?? string.Empty,
            first?.SourceName ?? source.SourcePath, first?.ImportFormat ?? ChapterImportFormat.Unknown,
            durationTicks > 0 ? ChapterDuration.FromTicks(durationTicks) : ChapterDuration.Unknown, tracks);
    }

    /// <summary>Projects a selected track without borrowing another track's rate.</summary>
    public static ChapterSet ToChapterSet(EditableChapterDocument document, int trackIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (trackIndex < 0 || trackIndex >= document.Tracks.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        }

        var track = document.Tracks[trackIndex];
        var segment = track.Segments.FirstOrDefault();
        var duration = TrackDuration(document, trackIndex);
        return new ChapterSet(segment?.Name ?? track.Name, segment?.SourceName ?? document.SourceName,
            segment?.ImportFormat ?? document.ImportFormat, track.FrameRate,
            duration.IsKnown ? TimeSpan.FromTicks(duration.Ticks) : TimeSpan.Zero,
            track.Chapters.Select(ToChapter).ToImmutableArray());
    }

    /// <summary>Applies a row edit while preserving other tracks and segment provenance.</summary>
    public static EditableChapterDocument ApplyTrackChapterSetResult(EditableChapterDocument source, int trackIndex, ChapterSet result)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (trackIndex < 0 || trackIndex >= source.Tracks.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        }

        var track = source.Tracks[trackIndex];
        var chapters = result.Chapters.Select((chapter, index) => FromChapter(chapter,
            index < track.Chapters.Length ? track.Chapters[index].Id : ChapterId.New())).ToImmutableArray();
        var updated = new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments, result.FrameRate);
        return new EditableChapterDocument(source.Id, source.Title, source.SourceName, source.ImportFormat,
            source.Duration, source.Tracks.SetItem(trackIndex, updated));
    }

    /// <summary>Creates a document from canonical chapter-set data.</summary>
    public static EditableChapterDocument FromChapterSet(ChapterSet chapterSet)
    {
        ArgumentNullException.ThrowIfNull(chapterSet);
        var duration = chapterSet.Duration.Ticks > 0 ? ChapterDuration.FromTicks(chapterSet.Duration.Ticks) : ChapterDuration.Unknown;
        var chapters = chapterSet.Chapters.Select(static chapter => FromChapter(chapter, ChapterId.New()));
        var track = new EditableChapterTrack(ChapterTrackId.New(), chapterSet.Title, chapters, frameRate: chapterSet.FrameRate);
        return new EditableChapterDocument(ChapterDocumentId.New(), chapterSet.Title, chapterSet.SourceName,
            chapterSet.ImportFormat, duration, [track]);
    }

    /// <summary>Creates a scalar projection. Its rate is the uniform rate of all contributors.</summary>
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

            chapters.AddRange(track.Chapters.Select(ToChapter));
            hasPreviousTrack |= !track.Chapters.IsEmpty;
        }

        return new ChapterSet(document.Title, document.SourceName, document.ImportFormat, document.FrameRate,
            document.Duration.IsKnown ? TimeSpan.FromTicks(document.Duration.Ticks) : TimeSpan.Zero, chapters.ToImmutable());
    }

    /// <summary>Applies a same-row-count edit while preserving chapter identities.</summary>
    public static EditableChapterDocument ApplyNonStructuralChapterSetResult(EditableChapterDocument source, ChapterSet result)
    {
        RequireSingleTrack(source);
        if (source.Tracks[0].Chapters.Length != result.Chapters.Count)
        {
            throw new ArgumentException("A non-structural edit must preserve the row count.", nameof(result));
        }

        return ApplyLegacyChapterSetResult(source, result);
    }

    /// <summary>Applies converted chapters and absolute segment boundaries without changing source rates.</summary>
    public static EditableChapterDocument ApplyFrameRateChangeResult(EditableChapterDocument source, ChapterSet result,
        FrameRateValue sourceFps, FrameRateValue targetFps)
    {
        RequireSingleTrack(source);
        var updated = ApplyLegacyChapterSetResult(source, result);
        var track = updated.Tracks[0];
        var segments = track.Segments.Select(segment =>
        {
            var start = ChapterFpsTransformService.ConvertTicks(segment.StartTicks, sourceFps, targetFps);
            var duration = segment.Duration.IsKnown
                ? ChapterDuration.FromTicks(checked(ChapterFpsTransformService.ConvertTicks(
                    checked(segment.StartTicks + segment.Duration.Ticks), sourceFps, targetFps) - start))
                : ChapterDuration.Unknown;
            if (segment.Duration.IsKnown && segment.Duration.Ticks > 0 && duration.Ticks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetFps), "The conversion collapses a segment.");
            }

            return segment with { StartTicks = start, Duration = duration, FrameRate = targetFps };
        }).ToImmutableArray();
        var convertedDuration = source.Duration.IsKnown
            ? ChapterDuration.FromTicks(ChapterFpsTransformService.ConvertTicks(source.Duration.Ticks, sourceFps, targetFps))
            : ChapterDuration.Unknown;
        return new EditableChapterDocument(updated.Id, updated.Title, updated.SourceName, updated.ImportFormat,
            convertedDuration, [new EditableChapterTrack(track.Id, source.Tracks[0].Name, track.Chapters, segments, targetFps)]);
    }

    /// <summary>Applies a scalar result without rebuilding current or source rates from numbers.</summary>
    public static EditableChapterDocument ApplyLegacyChapterSetResult(EditableChapterDocument source, ChapterSet result)
    {
        RequireSingleTrack(source);
        var track = source.Tracks[0];
        var chapters = result.Chapters.Select((chapter, index) => FromChapter(chapter,
            index < track.Chapters.Length ? track.Chapters[index].Id : ChapterId.New()));
        var duration = result.Duration.Ticks > 0 ? ChapterDuration.FromTicks(result.Duration.Ticks) : source.Duration;
        return new EditableChapterDocument(source.Id, result.Title, result.SourceName, result.ImportFormat,
            duration, [new EditableChapterTrack(track.Id, result.Title, chapters, track.Segments, result.FrameRate)]);
    }

    private static void RequireSingleTrack(EditableChapterDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Tracks.Length != 1)
        {
            throw new ArgumentException("A scalar edit requires a single-track source document.", nameof(source));
        }
    }

    private static EditableChapter FromChapter(Chapter chapter, ChapterId id) =>
        new(id, chapter.DisplayNumber, chapter.StartTime.Ticks, chapter.Name, chapter.FramesInfo,
            chapter.EndTime?.Ticks, chapter.FrameAccuracy, chapter.Kind);

    private static Chapter ToChapter(EditableChapter chapter) =>
        new(chapter.DisplayNumber, TimeSpan.FromTicks(chapter.StartTicks), chapter.Name, chapter.FramesInfo,
            chapter.EndTicks is long end ? TimeSpan.FromTicks(end) : null, chapter.FrameAccuracy, chapter.Kind);
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

                if (chapter.EndTicks is { } endTicks && endTicks < chapter.StartTicks)
                {
                    errors.Add("Chapter end time must not precede its start time.");
                }
            }
        }

        return new EditableChapterDocumentValidationResult(errors.Count == 0, errors.ToImmutable());
    }
}

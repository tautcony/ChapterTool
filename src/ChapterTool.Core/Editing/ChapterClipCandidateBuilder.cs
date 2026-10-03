using System.Collections.Immutable;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

/// <summary>Builds merge, split, and append candidates over current document values.</summary>
public static class ChapterClipCandidateBuilder
{
    /// <summary>Merges selected tracks in timeline order and records only segment metadata.</summary>
    public static ChapterCandidateBuildResult MergeTracks(
        EditableChapterDocument source,
        IEnumerable<ChapterTrackId> selectedTrackIds)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selectedTrackIds);
        var selected = selectedTrackIds.ToHashSet();
        var tracks = source.Tracks.Where(track => selected.Contains(track.Id)).ToArray();
        if (tracks.Length < 2 || tracks.Length != selected.Count)
        {
            return Failure(source, "Select at least two existing tracks to merge.");
        }

        var rates = tracks.SelectMany(track => EffectiveRates(source, track)).Distinct().ToArray();
        if (rates.Length > 1)
        {
            return Failure(source, "Tracks with different effective frame rates cannot be merged.");
        }

        var offset = 0L;
        var chapters = ImmutableArray.CreateBuilder<EditableChapter>();
        var segments = ImmutableArray.CreateBuilder<EditableChapterSegment>();
        foreach (var track in tracks)
        {
            var duration = TrackDuration(source, track);
            if (duration is null)
            {
                return Failure(source, $"Track '{track.Name}' has no known duration for boundary placement.");
            }

            foreach (var chapter in track.Chapters)
            {
                try
                {
                    chapters.Add(chapter with { StartTicks = checked(chapter.StartTicks + offset), EndTicks = chapter.EndTicks is long end ? checked(end + offset) : null });
                }
                catch (OverflowException)
                {
                    return Failure(source, "Merging the selected tracks exceeds the supported time range.");
                }
            }

            var sourceSegments = track.Segments.IsEmpty
                ? [EditableChapterSegment.Create(
                    track.Id,
                    track.Id.Value.ToString("N"),
                    track.Name,
                    0,
                    ChapterDuration.FromTicks(duration.Value),
                    source.FrameRate,
                    source.ImportFormat,
                    source.SourceName)]
                : track.Segments;
            foreach (var segment in sourceSegments)
            {
                try
                {
                    segments.Add(segment with { StartTicks = checked(segment.StartTicks + offset) });
                }
                catch (OverflowException)
                {
                    return Failure(source, "Merging the selected boundaries exceeds the supported time range.");
                }
            }

            try
            {
                offset = checked(offset + duration.Value);
            }
            catch (OverflowException)
            {
                return Failure(source, "Merging the selected tracks exceeds the supported time range.");
            }
        }

        Renumber(chapters);
        var merged = new EditableChapterTrack(ChapterTrackId.New(), string.Join(" + ", tracks.Select(static track => track.Name)), chapters, segments);
        var remaining = source.Tracks.Where(track => !selected.Contains(track.Id)).ToImmutableArray();
        var output = remaining.Add(merged);
        var totalDuration = Math.Max(source.Duration.IsKnown ? source.Duration.Ticks : 0, offset);
        var candidate = new EditableChapterDocument(
            source.Id,
            source.Title,
            source.SourceName,
            source.ImportFormat,
            ChapterDuration.FromTicks(totalDuration),
            rates.FirstOrDefaultNullable(source.FrameRate),
            output);
        return Succeed(source, candidate);
    }

    /// <summary>Splits a current track at its stored boundaries without changing chapter identities.</summary>
    public static ChapterCandidateBuildResult SplitByBoundaries(EditableChapterDocument source, ChapterTrackId trackId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var trackIndex = IndexOf(source, trackId);
        if (trackIndex < 0)
        {
            return Failure(source, "The selected track does not exist.");
        }

        var track = source.Tracks[trackIndex];
        if (track.Segments.Length < 2 || track.Segments.Any(static segment => !segment.Duration.IsKnown))
        {
            return Failure(source, "The track does not have at least two known segment boundaries.");
        }

        var ordered = track.Segments.OrderBy(static segment => segment.StartTicks).ToArray();
        var chapterGroups = ordered.Select(_ => ImmutableArray.CreateBuilder<EditableChapter>()).ToArray();
        foreach (var chapter in track.Chapters)
        {
            var segmentIndex = FindSegment(ordered, chapter.StartTicks, source.Duration);
            if (segmentIndex < 0)
            {
                return Failure(source, $"Chapter '{chapter.Name}' does not start inside a segment interval.");
            }

            var segment = ordered[segmentIndex];
            long segmentEnd;
            try
            {
                segmentEnd = checked(segment.StartTicks + segment.Duration.Ticks);
            }
            catch (OverflowException)
            {
                return Failure(source, "A segment boundary exceeds the supported time range.");
            }

            if (chapter.EndTicks is long end && end > segmentEnd)
            {
                return Failure(source, $"Chapter '{chapter.Name}' has an explicit end time that crosses a segment boundary.");
            }

            chapterGroups[segmentIndex].Add(chapter with
            {
                StartTicks = chapter.StartTicks - segment.StartTicks,
                EndTicks = chapter.EndTicks is long explicitEnd ? explicitEnd - segment.StartTicks : null
            });
        }

        var splitTracks = ImmutableArray.CreateBuilder<EditableChapterTrack>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var segment = ordered[index] with { StartTicks = 0 };
            var segmentChapters = chapterGroups[index];
            Renumber(segmentChapters);
            splitTracks.Add(new EditableChapterTrack(segment.SourceTrackId, segment.Name, segmentChapters, [segment]));
        }

        var output = source.Tracks.RemoveAt(trackIndex).InsertRange(trackIndex, splitTracks);
        return Succeed(source, new EditableChapterDocument(
            source.Id,
            source.Title,
            source.SourceName,
            source.ImportFormat,
            source.Duration,
            source.FrameRate,
            output));
    }

    /// <summary>Appends imported content to a current single-track document with fresh chapter identities.</summary>
    public static ChapterCandidateBuildResult Append(
        EditableChapterDocument current,
        EditableChapterDocument imported)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(imported);
        var original = current;
        if (current.Tracks.Length > 1)
        {
            var merged = MergeTracks(current, current.Tracks.Select(static track => track.Id));
            if (!merged.IsValid)
            {
                return Failure(original, merged.Errors);
            }

            current = merged.Candidate;
        }

        if (current.Tracks.Length != 1 || current.Tracks[0].Id.Value == Guid.Empty)
        {
            return Failure(original, "Append requires current chapter content.");
        }

        if (!current.Duration.IsKnown || !imported.Duration.IsKnown)
        {
            return Failure(original, "Append requires known durations for both documents.");
        }

        var currentTrack = current.Tracks[0];
        var importedRates = imported.Tracks.SelectMany(track => EffectiveRates(imported, track)).Distinct().ToArray();
        var currentRates = EffectiveRates(current, currentTrack).Distinct().ToArray();
        if (currentRates.Concat(importedRates).Distinct().Count() > 1)
        {
            return Failure(original, "Documents with different effective frame rates cannot be appended.");
        }

        var offset = current.Duration.Ticks;
        var chapters = currentTrack.Chapters.ToBuilder();
        var segments = currentTrack.Segments.ToBuilder();
        if (segments.Count == 0)
        {
            segments.Add(EditableChapterSegment.Create(
                currentTrack.Id,
                currentTrack.Id.Value.ToString("N"),
                currentTrack.Name,
                0,
                current.Duration,
                current.FrameRate,
                current.ImportFormat,
                current.SourceName));
        }

        var appendOffset = offset;
        long importedDuration = 0;
        foreach (var track in imported.Tracks)
        {
            var trackDuration = TrackDuration(imported, track);
            if (trackDuration is null)
            {
                return Failure(original, $"Imported track '{track.Name}' has no known duration.");
            }

            foreach (var chapter in track.Chapters)
            {
                try
                {
                    chapters.Add(chapter with
                    {
                        Id = ChapterId.New(),
                        StartTicks = checked(chapter.StartTicks + appendOffset),
                        EndTicks = chapter.EndTicks is long end ? checked(end + appendOffset) : null
                    });
                }
                catch (OverflowException)
                {
                    return Failure(original, "Appending exceeds the supported time range.");
                }
            }

            var trackSegments = track.Segments.IsEmpty
                ? [EditableChapterSegment.Create(
                        ChapterTrackId.New(),
                        track.Id.Value.ToString("N"),
                        track.Name,
                        0,
                        ChapterDuration.FromTicks(trackDuration.Value),
                        imported.FrameRate,
                        imported.ImportFormat,
                        imported.SourceName)]
                : track.Segments;
            foreach (var segment in trackSegments)
            {
                try
                {
                    segments.Add(segment with
                    {
                        Id = Guid.NewGuid(),
                        SourceTrackId = ChapterTrackId.New(),
                        StartTicks = checked(segment.StartTicks + appendOffset)
                    });
                }
                catch (OverflowException)
                {
                    return Failure(original, "Appending segment boundaries exceeds the supported time range.");
                }
            }

            try
            {
                appendOffset = checked(appendOffset + trackDuration.Value);
                importedDuration = checked(importedDuration + trackDuration.Value);
            }
            catch (OverflowException)
            {
                return Failure(original, "Appending exceeds the supported time range.");
            }
        }

        Renumber(chapters);
        long duration;
        try
        {
            duration = checked(current.Duration.Ticks + importedDuration);
        }
        catch (OverflowException)
        {
            return Failure(original, "Appending exceeds the supported time range.");
        }

        var combined = new EditableChapterTrack(currentTrack.Id, currentTrack.Name, chapters, segments);
        var candidate = new EditableChapterDocument(
            current.Id,
            current.Title,
            current.SourceName,
            current.ImportFormat,
            ChapterDuration.FromTicks(duration),
            current.FrameRate,
            [combined]);
        return Succeed(original, candidate);
    }

    private static IEnumerable<ChapterFrameRate> EffectiveRates(EditableChapterDocument document, EditableChapterTrack track)
    {
        if (track.Segments.IsEmpty)
        {
            if (document.FrameRate is ChapterFrameRate rate)
            {
                yield return rate;
            }

            yield break;
        }

        foreach (var segment in track.Segments)
        {
            if ((segment.FrameRate ?? document.FrameRate) is ChapterFrameRate rate)
            {
                yield return rate;
            }
        }
    }

    private static long? TrackDuration(EditableChapterDocument document, EditableChapterTrack track)
    {
        if (track.Segments.IsEmpty)
        {
            return document.Duration.IsKnown ? document.Duration.Ticks : null;
        }

        if (track.Segments.Any(static segment => !segment.Duration.IsKnown))
        {
            return null;
        }

        try
        {
            return track.Segments.Max(static segment => checked(segment.StartTicks + segment.Duration.Ticks));
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static int FindSegment(EditableChapterSegment[] segments, long startTicks, ChapterDuration totalDuration)
    {
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var end = checked(segment.StartTicks + segment.Duration.Ticks);
            var isFinalEndMarker = totalDuration.IsKnown
                && startTicks == totalDuration.Ticks
                && index == segments.Length - 1
                && end == totalDuration.Ticks;
            if ((startTicks >= segment.StartTicks && startTicks < end) || isFinalEndMarker)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOf(EditableChapterDocument source, ChapterTrackId id)
    {
        for (var index = 0; index < source.Tracks.Length; index++)
        {
            if (source.Tracks[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void Renumber(ImmutableArray<EditableChapter>.Builder chapters)
    {
        var number = 0;
        for (var index = 0; index < chapters.Count; index++)
        {
            chapters[index] = chapters[index] with { DisplayNumber = chapters[index].Kind == ChapterKind.Separator ? 0 : ++number };
        }
    }

    private static ChapterCandidateBuildResult Succeed(EditableChapterDocument source, EditableChapterDocument candidate)
    {
        var validation = EditableChapterDocumentValidator.Validate(candidate);
        return validation.IsValid
            ? new ChapterCandidateBuildResult(true, candidate, source.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id).ToImmutableHashSet(), [])
            : Failure(source, validation.Errors);
    }

    private static ChapterCandidateBuildResult Failure(EditableChapterDocument source, string error) => Failure(source, [error]);

    private static ChapterCandidateBuildResult Failure(EditableChapterDocument source, IEnumerable<string> errors) =>
        new(false, source, [], errors.ToImmutableArray());
}

internal static class ChapterFrameRateEnumerableExtensions
{
    public static ChapterFrameRate? FirstOrDefaultNullable(this IEnumerable<ChapterFrameRate> rates, ChapterFrameRate? fallback)
    {
        using var enumerator = rates.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current : fallback;
    }
}

using System.Collections.Immutable;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Models;

/// <summary>Converts between the immutable document model and the legacy ChapterSet contract.</summary>
public static class EditableChapterDocumentAdapter
{
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
        return new EditableChapterDocument(
            source.Id,
            result.Title,
            result.SourceName,
            result.ImportFormat,
            duration,
            FromLegacyFrameRate(result.FramesPerSecond),
            [new EditableChapterTrack(source.Tracks[0].Id, result.Title, chapters)]);
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

                if (document.Duration.IsKnown && chapter.StartTicks > document.Duration.Ticks)
                {
                    errors.Add("Chapter start time exceeds the known document duration.");
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

using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Editing;

/// <summary>Builds complete immutable candidates for editable chapter content operations.</summary>
public sealed partial class ChapterContentCandidateBuilder(
    IChapterEditingService editingService,
    ChapterExpressionService? expressionService = null)
{
    private readonly ChapterExpressionService expressionService = expressionService ?? new ChapterExpressionService();

    /// <summary>Builds a candidate for a chapter name or start-time cell edit.</summary>
    public ChapterCandidateBuildResult EditCell(
        EditableChapterDocument source,
        ChapterId chapterId,
        ChapterCellField field,
        string value,
        decimal framesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(value);
        var location = Locate(source, chapterId);
        if (location is null)
        {
            return Failure(source, "The target chapter no longer exists.", chapterId);
        }

        var legacy = EditableChapterDocumentAdapter.ToChapterSet(source);
        var result = field switch
        {
            ChapterCellField.Name => editingService.Rename(legacy, location.Value.Index, value),
            ChapterCellField.StartTime => editingService.EditTime(legacy, location.Value.Index, value),
            ChapterCellField.Frame => editingService.EditFrame(legacy, location.Value.Index, value, framesPerSecond),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        if (result.Diagnostics.Count != 0)
        {
            return Failure(source, result.Diagnostics.Select(static diagnostic => diagnostic.Message), [chapterId]);
        }

        return Success(EditableChapterDocumentAdapter.ApplyNonStructuralChapterSetResult(source, result.ChapterSet), [chapterId]);
    }

    /// <summary>Builds an insertion candidate before the specified chapter, or at the end when the ID is null.</summary>
    public ChapterCandidateBuildResult InsertBefore(EditableChapterDocument source, ChapterId? beforeChapterId, string name = "New Chapter")
    {
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var track = trackResult.Track!;
        var index = beforeChapterId is null ? track.Chapters.Length : IndexOf(track, beforeChapterId.Value);
        if (index < 0)
        {
            return Failure(source, "The insertion target no longer exists.", beforeChapterId);
        }

        var chapters = track.Chapters.ToBuilder();
        chapters.Insert(index, new EditableChapter(ChapterId.New(), 0, 0, name));
        Renumber(chapters);
        return Success(WithTrack(source, WithChapters(track, chapters.ToImmutable())), beforeChapterId is null ? [] : [beforeChapterId.Value]);
    }

    /// <summary>Builds a delete candidate from captured chapter identities.</summary>
    public ChapterCandidateBuildResult Delete(
        EditableChapterDocument source,
        IReadOnlySet<ChapterId> chapterIds,
        ChapterEditingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chapterIds);
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var track = trackResult.Track!;
        if (chapterIds.Any(id => IndexOf(track, id) < 0))
        {
            return Failure(source, "A captured delete target no longer exists.", chapterIds);
        }

        var legacy = EditableChapterDocumentAdapter.ToChapterSet(source);
        var indexes = track.Chapters.Select((chapter, index) => (chapter, index))
            .Where(item => chapterIds.Contains(item.chapter.Id))
            .Select(static item => item.index)
            .ToHashSet();
        var edited = editingService.Delete(legacy, indexes, options);
        var retained = track.Chapters.Where(chapter => !chapterIds.Contains(chapter.Id)).ToArray();
        if (edited.ChapterSet.Chapters.Count != retained.Length)
        {
            return Failure(source, "The delete operation returned an incomplete candidate.", chapterIds);
        }

        var chapters = edited.ChapterSet.Chapters.Select((chapter, index) => FromLegacy(retained[index].Id, chapter)).ToImmutableArray();
        if (options?.DeleteRowsTiming == DeleteRowsTimingMode.Normalize && indexes.Contains(0) && retained.Length > 0)
        {
            var shift = retained[0].StartTicks;
            chapters = chapters.Select(chapter => chapter with
            {
                StartTicks = checked(chapter.StartTicks - shift),
                EndTicks = chapter.EndTicks is long end ? checked(end - shift) : null
            }).ToImmutableArray();
        }

        return ValidateAndSucceed(WithTrack(source, new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)), chapterIds);
    }

    /// <summary>Builds a candidate whose chapter order exactly matches the captured ID sequence.</summary>
    public ChapterCandidateBuildResult Reorder(EditableChapterDocument source, IReadOnlyList<ChapterId> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var track = trackResult.Track!;
        if (orderedIds.Count != track.Chapters.Length || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => IndexOf(track, id) < 0))
        {
            return Failure(source, "The reorder target must list every chapter exactly once.", orderedIds);
        }

        var byId = track.Chapters.ToDictionary(static chapter => chapter.Id);
        var chapters = Renumber(orderedIds.Select(id => byId[id]).ToImmutableArray());
        return ValidateAndSucceed(WithTrack(source, new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)), orderedIds);
    }

    /// <summary>Applies display numbering to every chapter as a one-shot content edit.</summary>
    public ChapterCandidateBuildResult ApplyNumbering(EditableChapterDocument source, int orderShift)
    {
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var chapters = trackResult.Track!.Chapters.ToBuilder();
        var shift = Math.Max(0, orderShift);
        var number = 0;
        for (var index = 0; index < chapters.Count; index++)
        {
            var chapter = chapters[index];
            chapters[index] = chapter with { DisplayNumber = chapter.Kind == ChapterKind.Separator ? 0 : checked(++number + shift) };
        }

        return ValidateAndSucceed(WithTrack(source, WithChapters(trackResult.Track, chapters.ToImmutable())), trackResult.Track.Chapters.Select(static chapter => chapter.Id));
    }

    /// <summary>Applies template names to captured chapter identities.</summary>
    public ChapterCandidateBuildResult ApplyTemplateNames(
        EditableChapterDocument source,
        string templateText,
        IReadOnlySet<ChapterId>? targetIds = null)
    {
        ArgumentNullException.ThrowIfNull(templateText);
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var track = trackResult.Track!;
        var targets = targetIds ?? track.Chapters.Select(static chapter => chapter.Id).ToHashSet();
        if (targets.Any(id => IndexOf(track, id) < 0))
        {
            return Failure(source, "A captured template target no longer exists.", targets);
        }

        var names = TemplateNames(templateText);
        var chapters = track.Chapters.ToBuilder();
        var templateIndex = 0;
        for (var index = 0; index < chapters.Count; index++)
        {
            var chapter = chapters[index];
            if (!targets.Contains(chapter.Id) || chapter.Kind == ChapterKind.Separator)
            {
                continue;
            }

            if (templateIndex < names.Length && names[templateIndex].Length > 0)
            {
                chapters[index] = chapter with { Name = names[templateIndex] };
            }

            templateIndex++;
        }

        return ValidateAndSucceed(WithTrack(source, WithChapters(track, chapters.ToImmutable())), targets);
    }

    /// <summary>Applies a regular-expression replacement to chapter names as one candidate.</summary>
    public ChapterCandidateBuildResult ReplaceNames(
        EditableChapterDocument source,
        string pattern,
        string replacement,
        IReadOnlySet<ChapterId>? targetIds = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        try
        {
            var trackResult = RequireSingleTrack(source);
            if (trackResult.Error is not null)
            {
                return Failure(source, trackResult.Error);
            }

            var track = trackResult.Track!;
            var targets = targetIds ?? track.Chapters.Select(static chapter => chapter.Id).ToHashSet();
            if (targets.Any(id => IndexOf(track, id) < 0))
            {
                return Failure(source, "A captured replacement target no longer exists.", targets);
            }

            var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var chapters = track.Chapters.Select(chapter => targets.Contains(chapter.Id)
                ? chapter with { Name = regex.Replace(chapter.Name, replacement) }
                : chapter).ToImmutableArray();
            return ValidateAndSucceed(WithTrack(source, new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)), targets);
        }
        catch (ArgumentException exception)
        {
            return Failure(source, exception.Message, targetIds ?? ImmutableHashSet<ChapterId>.Empty);
        }
        catch (RegexMatchTimeoutException exception)
        {
            return Failure(source, exception.Message, targetIds ?? ImmutableHashSet<ChapterId>.Empty);
        }
    }

    /// <summary>Applies a checked time offset to target starts and explicit ends.</summary>
    public ChapterCandidateBuildResult Offset(
        EditableChapterDocument source,
        IReadOnlySet<ChapterId> targetIds,
        TimeSpan offset)
    {
        ArgumentNullException.ThrowIfNull(targetIds);
        var trackResult = RequireSingleTrack(source);
        if (trackResult.Error is not null)
        {
            return Failure(source, trackResult.Error);
        }

        var track = trackResult.Track!;
        if (targetIds.Any(id => IndexOf(track, id) < 0))
        {
            return Failure(source, "A captured offset target no longer exists.", targetIds);
        }

        try
        {
            var chapters = track.Chapters.Select(chapter => !targetIds.Contains(chapter.Id)
                ? chapter
                : chapter with
                {
                    StartTicks = checked(chapter.StartTicks + offset.Ticks),
                    EndTicks = chapter.EndTicks is long end ? checked(end + offset.Ticks) : null
                }).ToImmutableArray();
            return ValidateAndSucceed(WithTrack(source, new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)), targetIds);
        }
        catch (OverflowException)
        {
            return Failure(source, "The time offset exceeds the supported time range.", targetIds);
        }
    }

    /// <summary>Shifts selected chapter starts and explicit ends by a frame count.</summary>
    public ChapterCandidateBuildResult ShiftFrames(
        EditableChapterDocument source,
        IReadOnlySet<ChapterId> targetIds,
        int frames,
        decimal framesPerSecond)
    {
        if (framesPerSecond <= 0)
        {
            return Failure(source, "Frame rate must be greater than zero.", targetIds);
        }

        try
        {
            var ticks = ChapterRounding.RoundToInt64((decimal)frames * TimeSpan.TicksPerSecond / framesPerSecond);
            var shift = checked(-ticks);
            var trackResult = RequireSingleTrack(source);
            if (trackResult.Error is not null)
            {
                return Failure(source, trackResult.Error, targetIds);
            }

            var track = trackResult.Track!;
            if (targetIds.Any(id => IndexOf(track, id) < 0))
            {
                return Failure(source, "A captured frame-offset target no longer exists.", targetIds);
            }

            var chapters = track.Chapters
                .Where(chapter => !targetIds.Contains(chapter.Id) || checked(chapter.StartTicks + shift) >= 0)
                .Select(chapter => !targetIds.Contains(chapter.Id)
                    ? chapter
                    : chapter with
                    {
                        StartTicks = checked(chapter.StartTicks + shift),
                        EndTicks = chapter.EndTicks is long end ? checked(end + shift) : null
                    })
                .ToImmutableArray();
            chapters = Renumber(chapters);
            return ValidateAndSucceed(WithTrack(source, new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)), targetIds);
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            return Failure(source, "The frame offset exceeds the supported time range.", targetIds);
        }
    }

    /// <summary>Changes frame rate and scales chapter starts, explicit ends, and known duration.</summary>
    public ChapterCandidateBuildResult ChangeFrameRate(EditableChapterDocument source, decimal sourceFps, decimal targetFps)
    {
        if (sourceFps <= 0 || targetFps <= 0)
        {
            return Failure(source, "Source and target frame rates must be greater than zero.");
        }

        try
        {
            var legacy = EditableChapterDocumentAdapter.ToChapterSet(source);
            var result = ChapterFpsTransformService.ChangeFps(legacy, sourceFps, targetFps);
            if (!result.Success || result.Diagnostics.Count > 0)
            {
                return Failure(source, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
            }

            var updated = EditableChapterDocumentAdapter.ApplyFrameRateChangeResult(source, result.Info, sourceFps, targetFps);
            return ValidateAndSucceed(updated, source.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id));
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            return Failure(source, "The frame-rate conversion exceeds the supported time range.");
        }
    }

    /// <summary>Applies a chapter expression to every non-separator chapter and rejects any row diagnostic.</summary>
    public ChapterCandidateBuildResult ApplyExpression(EditableChapterDocument source, string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var result = expressionService.Apply(EditableChapterDocumentAdapter.ToChapterSet(source), true, expression);
        if (result.Diagnostics.Count > 0)
        {
            return Failure(source, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        return ValidateAndSucceed(
            EditableChapterDocumentAdapter.ApplyNonStructuralChapterSetResult(source, result.Info),
            source.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id));
    }

    /// <summary>Applies expression, naming, and numbering parameters once as one complete candidate.</summary>
    public ChapterCandidateBuildResult ApplyOutputOptions(
        EditableChapterDocument source,
        bool autoGenerateNames,
        bool useTemplateNames,
        string templateText,
        int orderShift,
        bool applyExpression,
        string expression)
    {
        var candidate = source;
        var targetIds = source.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id).ToHashSet();
        if (applyExpression)
        {
            var expressionResult = ApplyExpression(candidate, expression);
            if (!expressionResult.IsValid)
            {
                return Failure(source, expressionResult.Errors, targetIds);
            }

            candidate = expressionResult.Candidate;
        }

        if (autoGenerateNames || useTemplateNames)
        {
            var effectiveTemplate = string.IsNullOrWhiteSpace(templateText) && (autoGenerateNames || useTemplateNames)
                ? string.Join(Environment.NewLine, Enumerable.Range(1, source.Tracks.SelectMany(static track => track.Chapters).Count(static chapter => chapter.Kind != ChapterKind.Separator)).Select(static index => $"Chapter {index:D2}"))
                : templateText;
            var names = ApplyTemplateNames(candidate, effectiveTemplate, targetIds);
            if (!names.IsValid)
            {
                return Failure(source, names.Errors, targetIds);
            }

            candidate = names.Candidate;
        }

        if (orderShift != 0)
        {
            var numbering = ApplyNumbering(candidate, orderShift);
            if (!numbering.IsValid)
            {
                return Failure(source, numbering.Errors, targetIds);
            }

            candidate = numbering.Candidate;
        }

        return ValidateAndSucceed(candidate, targetIds);
    }

    /// <summary>Changes title and source metadata in one candidate.</summary>
    public ChapterCandidateBuildResult UpdateMetadata(
        EditableChapterDocument source,
        string? title = null,
        string? sourceName = null,
        string? trackName = null,
        bool updateTitle = false,
        bool updateSourceName = false,
        bool updateTrackName = false)
    {
        if (source.Tracks.IsDefaultOrEmpty)
        {
            return Failure(source, "The document has no editable track metadata.");
        }

        var first = source.Tracks[0];
        var tracks = source.Tracks.SetItem(0, new EditableChapterTrack(
            first.Id,
            updateTrackName ? trackName ?? string.Empty : first.Name,
            first.Chapters,
            first.Segments));
        var candidate = new EditableChapterDocument(
            source.Id,
            updateTitle ? title ?? string.Empty : source.Title,
            updateSourceName ? sourceName : source.SourceName,
            source.ImportFormat,
            source.Duration,
            source.FrameRate,
            tracks);
        return ValidateAndSucceed(candidate, []);
    }

    private static ChapterCandidateBuildResult ValidateAndSucceed(EditableChapterDocument candidate, IEnumerable<ChapterId> targets)
    {
        var validation = EditableChapterDocumentValidator.Validate(candidate);
        return validation.IsValid
            ? Success(candidate, targets)
            : Failure(candidate, validation.Errors, targets);
    }

    private static ChapterCandidateBuildResult Success(EditableChapterDocument candidate, IEnumerable<ChapterId> targets) =>
        new(true, candidate, targets.ToImmutableHashSet(), []);

    private static ChapterCandidateBuildResult Failure(EditableChapterDocument source, string error, object? target = null) =>
        new(false, source, target is ChapterId chapterId ? ImmutableHashSet.Create(chapterId) : [], [error]);

    private static ChapterCandidateBuildResult Failure(EditableChapterDocument source, IEnumerable<string> errors, IEnumerable<ChapterId>? targets = null) =>
        new(false, source, targets?.ToImmutableHashSet() ?? [], errors.ToImmutableArray());

    private static (EditableChapterTrack? Track, string? Error) RequireSingleTrack(EditableChapterDocument source) => source.Tracks.Length == 1
        ? (source.Tracks[0], null)
        : (null, "This operation requires one active chapter track.");

    private static (EditableChapterTrack Track, int Index)? Locate(EditableChapterDocument source, ChapterId id)
    {
        for (var trackIndex = 0; trackIndex < source.Tracks.Length; trackIndex++)
        {
            var track = source.Tracks[trackIndex];
            var chapterIndex = IndexOf(track, id);
            if (chapterIndex >= 0)
            {
                return (track, chapterIndex);
            }
        }

        return null;
    }

    private static int IndexOf(EditableChapterTrack track, ChapterId chapterId)
    {
        for (var index = 0; index < track.Chapters.Length; index++)
        {
            if (track.Chapters[index].Id == chapterId)
            {
                return index;
            }
        }

        return -1;
    }

    private static EditableChapterDocument WithTrack(EditableChapterDocument source, EditableChapterTrack track) =>
        new(source.Id, source.Title, source.SourceName, source.ImportFormat, source.Duration, source.FrameRate, [track]);

    private static EditableChapter FromLegacy(ChapterId id, Chapter chapter) => new(
        id,
        chapter.DisplayNumber,
        chapter.StartTime.Ticks,
        chapter.Name,
        chapter.FramesInfo,
        chapter.EndTime?.Ticks,
        chapter.FrameAccuracy,
        chapter.Kind);

    private static void Renumber(ImmutableArray<EditableChapter>.Builder chapters)
    {
        var number = 0;
        for (var index = 0; index < chapters.Count; index++)
        {
            var chapter = chapters[index];
            chapters[index] = chapter with { DisplayNumber = chapter.Kind == ChapterKind.Separator ? 0 : ++number };
        }
    }

    private static ImmutableArray<EditableChapter> Renumber(ImmutableArray<EditableChapter> chapters)
    {
        var number = 0;
        return chapters.Select(chapter => chapter with
        {
            DisplayNumber = chapter.Kind == ChapterKind.Separator ? 0 : ++number
        }).ToImmutableArray();
    }

    private static string[] TemplateNames(string text) => text.Trim(' ', '\r', '\n')
        .Split('\n')
        .Select(static line => line.TrimEnd('\r'))
        .ToArray();

    private static EditableChapterTrack WithChapters(EditableChapterTrack track, ImmutableArray<EditableChapter> chapters) =>
        new(track.Id, track.Name, chapters, track.Segments);
}

/// <summary>Identifies a cell value that can be edited by a chapter candidate.</summary>
public enum ChapterCellField
{
    /// <summary>The editable chapter name.</summary>
    Name,
    /// <summary>The chapter start time.</summary>
    StartTime,
    /// <summary>The chapter start frame.</summary>
    Frame
}

/// <summary>Describes a candidate document and any validation errors.</summary>
public sealed record ChapterCandidateBuildResult(
    bool IsValid,
    EditableChapterDocument Candidate,
    ImmutableHashSet<ChapterId> TargetIds,
    ImmutableArray<string> Errors);

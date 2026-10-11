using System.Collections.Immutable;

namespace ChapterTool.Core.Models;

/// <summary>Identifies an editable document.</summary>
/// <param name="Value">The document GUID.</param>
public readonly record struct ChapterDocumentId(Guid Value)
{
    /// <summary>Creates a new document identity.</summary>
    public static ChapterDocumentId New() => new(Guid.NewGuid());
}

/// <summary>Identifies a track within an editable document.</summary>
/// <param name="Value">The track GUID.</param>
public readonly record struct ChapterTrackId(Guid Value)
{
    /// <summary>Creates a new track identity.</summary>
    public static ChapterTrackId New() => new(Guid.NewGuid());
}

/// <summary>Identifies a chapter within an editable document.</summary>
/// <param name="Value">The chapter GUID.</param>
public readonly record struct ChapterId(Guid Value)
{
    /// <summary>Creates a new chapter identity.</summary>
    public static ChapterId New() => new(Guid.NewGuid());
}

/// <summary>A positive frame rate stored as a reduced rational number.</summary>
public readonly record struct ChapterFrameRate
{
    /// <summary>Creates a rate and reduces its numerator and denominator.</summary>
    /// <param name="numerator">The positive frame-rate numerator.</param>
    /// <param name="denominator">The positive frame-rate denominator.</param>
    public ChapterFrameRate(long numerator, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(denominator);

        var divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    /// <summary>Gets the reduced numerator.</summary>
    public long Numerator { get; }

    /// <summary>Gets the reduced denominator.</summary>
    public long Denominator { get; }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}

/// <summary>A known non-negative duration, or an explicit unknown duration.</summary>
public readonly record struct ChapterDuration
{
    private ChapterDuration(bool isKnown, long ticks)
    {
        IsKnown = isKnown;
        Ticks = ticks;
    }

    /// <summary>Gets whether the duration is known.</summary>
    public bool IsKnown { get; }

    /// <summary>Gets the duration in timestamp ticks, or zero when unknown.</summary>
    public long Ticks { get; }

    /// <summary>Gets the explicit unknown duration.</summary>
    public static ChapterDuration Unknown => new(false, 0);

    /// <summary>Creates a known duration from timestamp ticks.</summary>
    /// <param name="ticks">The non-negative duration in timestamp ticks.</param>
    /// <returns>The known duration.</returns>
    public static ChapterDuration FromTicks(long ticks) => ticks < 0
        ? throw new ArgumentOutOfRangeException(nameof(ticks))
        : new(true, ticks);
}

/// <summary>An immutable chapter value stored in an editable document.</summary>
/// <param name="Id">The stable chapter identity.</param>
/// <param name="DisplayNumber">The chapter number shown to users.</param>
/// <param name="StartTicks">The start time in timestamp ticks.</param>
/// <param name="Name">The chapter name.</param>
/// <param name="FramesInfo">The retained frame display text.</param>
/// <param name="EndTicks">The optional end time in timestamp ticks.</param>
/// <param name="FrameAccuracy">The accuracy of the stored frame information.</param>
/// <param name="Kind">The chapter kind.</param>
public sealed record EditableChapter(
    ChapterId Id,
    int DisplayNumber,
    long StartTicks,
    string Name,
    string FramesInfo = "",
    long? EndTicks = null,
    FrameAccuracy FrameAccuracy = FrameAccuracy.Neutral,
    ChapterKind Kind = ChapterKind.Marker);

/// <summary>An immutable ordered track of chapters.</summary>
public sealed class EditableChapterTrack
{
    /// <summary>Creates an immutable track snapshot.</summary>
    /// <param name="id">The track identity.</param>
    /// <param name="name">The track name.</param>
    /// <param name="chapters">The ordered chapters in the track.</param>
    /// <param name="segments">The source segments placed on the track.</param>
    /// <param name="frameRate">The explicit rate for a track without segments.</param>
    public EditableChapterTrack(
        ChapterTrackId id,
        string name,
        IEnumerable<EditableChapter> chapters,
        IEnumerable<EditableChapterSegment>? segments = null,
        FrameRateValue frameRate = default)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Chapters = chapters?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(chapters));
        Segments = segments?.ToImmutableArray() ?? [];
        FrameRate = Segments.IsEmpty ? frameRate : FrameRateValue.ResolveUniform(Segments.Select(static segment => segment.FrameRate));
    }

    /// <summary>Gets the track identity.</summary>
    public ChapterTrackId Id { get; }

    /// <summary>Gets the track name.</summary>
    public string Name { get; }

    /// <summary>Gets the chapters in display order.</summary>
    public ImmutableArray<EditableChapter> Chapters { get; }

    /// <summary>Gets source and placement metadata for the track's media segments.</summary>
    public ImmutableArray<EditableChapterSegment> Segments { get; }

    /// <summary>Gets the uniform current rate, including explicit metadata on a track without segments.</summary>
    public FrameRateValue FrameRate { get; }
}

/// <summary>Describes one source segment placed on an editable track timeline.</summary>
/// <param name="Id">The segment identity.</param>
/// <param name="SourceTrackId">The identity of the imported source track.</param>
/// <param name="SourceEntryId">The identity of the imported source entry.</param>
/// <param name="Name">The segment name.</param>
/// <param name="StartTicks">The segment start in timestamp ticks.</param>
/// <param name="Duration">The segment duration.</param>
/// <param name="FrameRate">The segment's current frame rate.</param>
/// <param name="ImportFormat">The format used to import the segment.</param>
/// <param name="SourceName">The source name, when available.</param>
/// <param name="ReferencedMediaFiles">The media files referenced by the segment.</param>
/// <param name="MediaTracks">The media tracks discovered in the source.</param>
/// <param name="ChapterCount">The number of imported chapters.</param>
/// <param name="ImportDisplayName">The localized format name captured at import time.</param>
/// <param name="DiscTitleNumber">The source disc title number, when applicable.</param>
/// <param name="SourceFrameRate">The source frame rate before placement transforms.</param>
/// <param name="SourceChapterCount">The source chapter count before transforms.</param>
public sealed record EditableChapterSegment(
    Guid Id,
    ChapterTrackId SourceTrackId,
    string SourceEntryId,
    string Name,
    long StartTicks,
    ChapterDuration Duration,
    FrameRateValue FrameRate,
    ChapterImportFormat ImportFormat,
    string? SourceName,
    ImmutableArray<ReferencedMediaFile> ReferencedMediaFiles,
    ImmutableArray<ChapterImportMediaTrack> MediaTracks,
    int ChapterCount = 0,
    string? ImportDisplayName = null,
    int? DiscTitleNumber = null,
    FrameRateValue SourceFrameRate = default,
    int? SourceChapterCount = null)
{
    /// <summary>Creates a segment with fresh structural identity and immutable media metadata.</summary>
    /// <param name="sourceTrackId">The source track identity.</param>
    /// <param name="sourceEntryId">The source entry identity.</param>
    /// <param name="name">The segment name.</param>
    /// <param name="startTicks">The segment start in timestamp ticks.</param>
    /// <param name="duration">The segment duration.</param>
    /// <param name="frameRate">The current segment frame rate.</param>
    /// <param name="importFormat">The format used to import the segment.</param>
    /// <param name="sourceName">The source name, when available.</param>
    /// <param name="referencedMediaFiles">The media files referenced by the segment.</param>
    /// <param name="mediaTracks">The source media tracks.</param>
    /// <param name="chapterCount">The number of chapters in the source entry.</param>
    /// <param name="importDisplayName">The localized import format name.</param>
    /// <param name="discTitleNumber">The source disc title number, when applicable.</param>
    /// <param name="sourceFrameRate">The source rate before transforms.</param>
    /// <param name="sourceChapterCount">The source chapter count before transforms.</param>
    /// <returns>The new segment value.</returns>
    public static EditableChapterSegment Create(
        ChapterTrackId sourceTrackId,
        string sourceEntryId,
        string name,
        long startTicks,
        ChapterDuration duration,
        FrameRateValue frameRate,
        ChapterImportFormat importFormat,
        string? sourceName,
        IEnumerable<ReferencedMediaFile>? referencedMediaFiles = null,
        IEnumerable<ChapterImportMediaTrack>? mediaTracks = null,
        int chapterCount = 0,
        string? importDisplayName = null,
        int? discTitleNumber = null,
        FrameRateValue? sourceFrameRate = null,
        int? sourceChapterCount = null) =>
        new(Guid.NewGuid(), sourceTrackId, sourceEntryId, name, startTicks, duration, frameRate, importFormat, sourceName,
            referencedMediaFiles?.ToImmutableArray() ?? [], mediaTracks?.ToImmutableArray() ?? [], chapterCount, importDisplayName,
            discTitleNumber, sourceFrameRate ?? frameRate, sourceChapterCount ?? chapterCount);
}

/// <summary>An immutable snapshot of editable chapter content and its metadata.</summary>
public sealed class EditableChapterDocument
{
    /// <summary>Creates an immutable document snapshot.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="title">The document title.</param>
    /// <param name="sourceName">The source name, when available.</param>
    /// <param name="importFormat">The format used to import the document.</param>
    /// <param name="duration">The document duration.</param>
    /// <param name="tracks">The ordered tracks in the document.</param>
    public EditableChapterDocument(
        ChapterDocumentId id,
        string title,
        string? sourceName,
        ChapterImportFormat importFormat,
        ChapterDuration duration,
        IEnumerable<EditableChapterTrack> tracks)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        SourceName = sourceName;
        ImportFormat = importFormat;
        Duration = duration;
        Tracks = tracks?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(tracks));
        FrameRate = FrameRateValue.ResolveUniform(Tracks.Select(static track => track.FrameRate));
    }

    /// <summary>Gets the document identity.</summary>
    public ChapterDocumentId Id { get; }

    /// <summary>Gets the document title.</summary>
    public string Title { get; }

    /// <summary>Gets the source name, when available.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the format used to import the document.</summary>
    public ChapterImportFormat ImportFormat { get; }

    /// <summary>Gets the document duration.</summary>
    public ChapterDuration Duration { get; }

    /// <summary>Gets the uniform frame rate shared by all tracks, or unknown otherwise.</summary>
    public FrameRateValue FrameRate { get; }

    /// <summary>Gets the tracks in document order.</summary>
    public ImmutableArray<EditableChapterTrack> Tracks { get; }
}

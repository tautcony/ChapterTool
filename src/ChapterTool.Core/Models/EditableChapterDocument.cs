using System.Collections.Immutable;

#pragma warning disable CS1591

namespace ChapterTool.Core.Models;

/// <summary>Identifies an editable document.</summary>
public readonly record struct ChapterDocumentId(Guid Value)
{
    /// <summary>Creates a new document identity.</summary>
    public static ChapterDocumentId New() => new(Guid.NewGuid());
}

/// <summary>Identifies a track within an editable document.</summary>
public readonly record struct ChapterTrackId(Guid Value)
{
    /// <summary>Creates a new track identity.</summary>
    public static ChapterTrackId New() => new(Guid.NewGuid());
}

/// <summary>Identifies a chapter within an editable document.</summary>
public readonly record struct ChapterId(Guid Value)
{
    /// <summary>Creates a new chapter identity.</summary>
    public static ChapterId New() => new(Guid.NewGuid());
}

/// <summary>A positive frame rate stored as a reduced rational number.</summary>
public readonly record struct ChapterFrameRate
{
    public ChapterFrameRate(long numerator, long denominator)
    {
        if (numerator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator));
        }

        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        var divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public long Numerator { get; }

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

    public bool IsKnown { get; }

    public long Ticks { get; }

    public static ChapterDuration Unknown => new(false, 0);

    public static ChapterDuration FromTicks(long ticks) => ticks < 0
        ? throw new ArgumentOutOfRangeException(nameof(ticks))
        : new(true, ticks);
}

/// <summary>An immutable chapter value stored in an editable document.</summary>
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
    public EditableChapterTrack(
        ChapterTrackId id,
        string name,
        IEnumerable<EditableChapter> chapters,
        IEnumerable<EditableChapterSegment>? segments = null)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Chapters = chapters?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(chapters));
        Segments = segments?.ToImmutableArray() ?? [];
    }

    public ChapterTrackId Id { get; }

    public string Name { get; }

    public ImmutableArray<EditableChapter> Chapters { get; }

    /// <summary>Gets source and placement metadata for the track's media segments.</summary>
    public ImmutableArray<EditableChapterSegment> Segments { get; }
}

/// <summary>Describes one source segment placed on an editable track timeline.</summary>
public sealed record EditableChapterSegment(
    Guid Id,
    ChapterTrackId SourceTrackId,
    string SourceEntryId,
    string Name,
    long StartTicks,
    ChapterDuration Duration,
    ChapterFrameRate? FrameRate,
    ChapterImportFormat ImportFormat,
    string? SourceName,
    ImmutableArray<ReferencedMediaFile> ReferencedMediaFiles,
    ImmutableArray<ChapterImportMediaTrack> MediaTracks,
    int ChapterCount = 0,
    string? ImportDisplayName = null,
    int? DiscTitleNumber = null,
    ChapterFrameRate? SourceFrameRate = null,
    int? SourceChapterCount = null)
{
    /// <summary>Creates a segment with fresh structural identity and immutable media metadata.</summary>
    public static EditableChapterSegment Create(
        ChapterTrackId sourceTrackId,
        string sourceEntryId,
        string name,
        long startTicks,
        ChapterDuration duration,
        ChapterFrameRate? frameRate,
        ChapterImportFormat importFormat,
        string? sourceName,
        IEnumerable<ReferencedMediaFile>? referencedMediaFiles = null,
        IEnumerable<ChapterImportMediaTrack>? mediaTracks = null,
        int chapterCount = 0,
        string? importDisplayName = null,
        int? discTitleNumber = null,
        ChapterFrameRate? sourceFrameRate = null,
        int? sourceChapterCount = null) =>
        new(Guid.NewGuid(), sourceTrackId, sourceEntryId, name, startTicks, duration, frameRate, importFormat, sourceName,
            referencedMediaFiles?.ToImmutableArray() ?? [], mediaTracks?.ToImmutableArray() ?? [], chapterCount, importDisplayName,
            discTitleNumber, sourceFrameRate ?? frameRate, sourceChapterCount ?? chapterCount);
}

/// <summary>An immutable snapshot of editable chapter content and its metadata.</summary>
public sealed class EditableChapterDocument
{
    public EditableChapterDocument(
        ChapterDocumentId id,
        string title,
        string? sourceName,
        ChapterImportFormat importFormat,
        ChapterDuration duration,
        ChapterFrameRate? frameRate,
        IEnumerable<EditableChapterTrack> tracks)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        SourceName = sourceName;
        ImportFormat = importFormat;
        Duration = duration;
        FrameRate = frameRate;
        Tracks = tracks?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(tracks));
    }

    public ChapterDocumentId Id { get; }

    public string Title { get; }

    public string? SourceName { get; }

    public ChapterImportFormat ImportFormat { get; }

    public ChapterDuration Duration { get; }

    public ChapterFrameRate? FrameRate { get; }

    public ImmutableArray<EditableChapterTrack> Tracks { get; }
}

#pragma warning restore CS1591

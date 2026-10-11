namespace ChapterTool.Core.Models;

// Preserve named arguments from the distributed positional record contract.
#pragma warning disable SA1313

/// <summary>Represents chapters and source metadata with one canonical current frame rate.</summary>
public sealed record ChapterSet
{
    /// <summary>Constructs the retained numeric contract. Numeric input has approximate authority.</summary>
    public ChapterSet(string Title, string? SourceName, ChapterImportFormat ImportFormat,
        double FramesPerSecond, TimeSpan Duration, IReadOnlyList<Chapter> Chapters,
        ChapterFrameRate? ExactFrameRate = null)
        : this(Title, SourceName, ImportFormat,
            ExactFrameRate is { } exact ? FrameRateValue.Rational(exact) : Normalize(FramesPerSecond), Duration, Chapters)
    {
    }

    /// <summary>Constructs canonical content without a numeric round-trip.</summary>
    public ChapterSet(string title, string? sourceName, ChapterImportFormat importFormat,
        FrameRateValue frameRate, TimeSpan duration, IReadOnlyList<Chapter> chapters)
    {
        this.Title = title;
        this.SourceName = sourceName;
        this.ImportFormat = importFormat;
        this.FrameRate = frameRate;
        this.Duration = duration;
        this.Chapters = chapters;
    }

    /// <summary>Gets the collection title.</summary>
    public string Title { get; init; }

    /// <summary>Gets the source file or stream name.</summary>
    public string? SourceName { get; init; }

    /// <summary>Gets the source format.</summary>
    public ChapterImportFormat ImportFormat { get; init; }

    /// <summary>Gets the canonical current frame rate.</summary>
    public FrameRateValue FrameRate { get; init; }

    /// <summary>Gets the legacy numeric projection. An assignment replaces rational authority.</summary>
    /// <remarks>This boundary remains supported through major version 23.</remarks>
    public double FramesPerSecond
    {
        get => FrameRate.ToDouble();
        init => FrameRate = Normalize(value);
    }

    /// <summary>Gets the current rational alias. It does not represent immutable source metadata.</summary>
    /// <remarks>This boundary remains supported through major version 23.</remarks>
    public ChapterFrameRate? ExactFrameRate
    {
        get => FrameRate.RationalRate;
        init => FrameRate = value is { } exact ? FrameRateValue.Rational(exact)
            : FrameRate.Kind == FrameRateKind.Rational ? Normalize(FrameRate.ToDouble()) : FrameRate;
    }

    /// <summary>Gets the legacy duration projection.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets the ordered chapters.</summary>
    public IReadOnlyList<Chapter> Chapters { get; init; }

    /// <summary>Deconstructs the retained positional record contract.</summary>
    public void Deconstruct(out string title, out string? sourceName, out ChapterImportFormat importFormat,
        out double framesPerSecond, out TimeSpan duration, out IReadOnlyList<Chapter> chapters,
        out ChapterFrameRate? exactFrameRate)
    {
        title = this.Title;
        sourceName = this.SourceName;
        importFormat = this.ImportFormat;
        framesPerSecond = this.FramesPerSecond;
        duration = this.Duration;
        chapters = this.Chapters;
        exactFrameRate = this.ExactFrameRate;
    }

    private static FrameRateValue Normalize(double value) =>
        FrameRateValue.TryFromDouble(value, metadata: true, out var rate, out var diagnostic)
            ? rate : throw new ArgumentOutOfRangeException(nameof(value), diagnostic!.Message);
}

#pragma warning restore SA1313

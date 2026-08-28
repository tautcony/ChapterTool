namespace ChapterTool.Core.Models;

/// <summary>
/// Describes one media track summary associated with an imported chapter entry.
/// </summary>
/// <param name="Kind">The semantic track kind, such as <c>video</c> or <c>audio</c>.</param>
/// <param name="Summary">The eac3to-like summary shown in logs and selectors.</param>
/// <param name="Codec">The normalized codec label, when known.</param>
/// <param name="Format">The normalized video or track format label, when known.</param>
/// <param name="Language">The normalized language tag, when known.</param>
/// <param name="Channels">The normalized channel layout label, when known.</param>
/// <param name="SampleRate">The normalized sample-rate label, when known.</param>
/// <param name="AspectRatio">The normalized aspect-ratio label, when known.</param>
public sealed record ChapterImportMediaTrack(
    string Kind,
    string Summary,
    string? Codec = null,
    string? Format = null,
    string? Language = null,
    string? Channels = null,
    string? SampleRate = null,
    string? AspectRatio = null);

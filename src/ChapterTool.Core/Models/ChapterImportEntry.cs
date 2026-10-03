namespace ChapterTool.Core.Models;

/// <summary>
/// Represents one chapter set entry discovered for an imported source.
/// </summary>
/// <param name="Id">The stable entry identifier within the imported source.</param>
/// <param name="DisplayName">The label shown when the user selects among imported entries.</param>
/// <param name="ChapterSet">The chapter data represented by this import entry.</param>
/// <param name="CanCombine">Whether this entry can be combined with sibling entries from the same source.</param>
/// <param name="ReferencedMediaFiles">The media files referenced by this chapter entry, when known.</param>
/// <param name="MediaTracks">The media track summaries associated with this chapter entry, when known.</param>
/// <param name="ImportDisplayName">The complete name used when presenting this entry in an import summary.</param>
/// <param name="DiscTitleNumber">The DVD disc title number used for a localized selection label, when known.</param>
/// <param name="ChapterCount">The current chapter count for metadata-only selector entries, when known.</param>
/// <param name="SourceChapterCount">The chapter count reported by the imported source, when known.</param>
public sealed record ChapterImportEntry(
    string Id,
    string DisplayName,
    ChapterSet ChapterSet,
    bool CanCombine = false,
    IReadOnlyList<ReferencedMediaFile>? ReferencedMediaFiles = null,
    IReadOnlyList<ChapterImportMediaTrack>? MediaTracks = null,
    string? ImportDisplayName = null,
    int? DiscTitleNumber = null,
    int? ChapterCount = null,
    int? SourceChapterCount = null)
{
    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

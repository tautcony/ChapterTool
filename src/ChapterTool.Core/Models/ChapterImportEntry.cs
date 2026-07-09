namespace ChapterTool.Core.Models;

/// <summary>
/// Represents one chapter set entry discovered for an imported source.
/// </summary>
/// <param name="Id">The Id value.</param>
/// <param name="DisplayName">The DisplayName value.</param>
/// <param name="ChapterSet">The ChapterSet value.</param>
/// <param name="CanCombine">The CanCombine value.</param>
/// <param name="MediaReferences">The MediaReferences value.</param>
public sealed record ChapterImportEntry(
    string Id,
    string DisplayName,
    ChapterSet ChapterSet,
    bool CanCombine = false,
    IReadOnlyList<MediaFileReference>? MediaReferences = null)
{
    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

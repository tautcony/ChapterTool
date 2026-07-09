namespace ChapterTool.Core.Models;

/// <summary>
/// Describes a source media file referenced by an imported chapter set.
/// </summary>
/// <param name="DisplayName">The file name or label shown for the referenced media file.</param>
/// <param name="RelativePath">The media path relative to the importing source, when available.</param>
/// <param name="AbsolutePath">The resolved absolute media path, when available.</param>
public sealed record ReferencedMediaFile(
    string DisplayName,
    string RelativePath,
    string? AbsolutePath = null);

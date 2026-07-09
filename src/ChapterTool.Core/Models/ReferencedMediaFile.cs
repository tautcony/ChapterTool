namespace ChapterTool.Core.Models;

/// <summary>
/// Describes a source media file referenced by an imported chapter set.
/// </summary>
/// <param name="DisplayName">The DisplayName value.</param>
/// <param name="RelativePath">The RelativePath value.</param>
/// <param name="AbsolutePath">The AbsolutePath value.</param>
public sealed record ReferencedMediaFile(
    string DisplayName,
    string RelativePath,
    string? AbsolutePath = null);

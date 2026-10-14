namespace ChapterTool.Core.Models;

/// <summary>
/// Contains the semantic values that hosts use to render one import option.
/// </summary>
/// <param name="MainText">The importer's display name.</param>
/// <param name="ChapterCount">The number of chapters in the option.</param>
public sealed record ChapterImportDisplay(string MainText, int ChapterCount)
{
    /// <summary>Creates display values from one imported entry.</summary>
    /// <param name="entry">The imported entry.</param>
    /// <returns>The display values for the entry.</returns>
    public static ChapterImportDisplay From(ChapterImportEntry entry) =>
        new(entry.DisplayName, entry.ChapterCount ?? entry.ChapterSet.Chapters.Count);

    /// <summary>Formats a DVD option with a host-supplied title label.</summary>
    /// <param name="name">The title label.</param>
    /// <param name="info">The chapter set.</param>
    /// <param name="mediaFiles">The referenced media files.</param>
    /// <returns>The complete option label.</returns>
    public static string FormatDvdOption(string name, ChapterSet info, IReadOnlyList<ReferencedMediaFile>? mediaFiles)
    {
        var titleSets = mediaFiles is { Count: > 0 }
            ? string.Join("+", mediaFiles.Select(static file => Path.GetFileNameWithoutExtension(file.DisplayName)).Distinct(StringComparer.OrdinalIgnoreCase))
            : info.SourceName;
        return $"{name} ({info.Duration.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)}) [{titleSets}]";
    }
}

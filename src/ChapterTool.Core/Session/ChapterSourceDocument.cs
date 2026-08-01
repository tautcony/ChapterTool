namespace ChapterTool.Core.Session;

/// <summary>Identifies a chapter source independently of the host storage model.</summary>
public abstract record ChapterSourceDocument(string DisplayName, string Identity);

/// <summary>Chapter source backed by a desktop path.</summary>
public sealed record LocalPathChapterSource(string Path, string? DisplayName = null)
    : ChapterSourceDocument(
        DisplayName ?? System.IO.Path.GetFileName(Path),
        System.IO.Path.GetFullPath(Path))
{
    /// <summary>Normalized local path used by desktop importers.</summary>
    public string NormalizedPath { get; } = System.IO.Path.GetFullPath(Path);
}

/// <summary>Chapter source backed by retained bytes from a portable host.</summary>
public sealed record BufferedChapterSource(string FileName, byte[] Content)
    : ChapterSourceDocument(FileName, $"buffer:{FileName}")
{
    /// <summary>Number of retained source bytes.</summary>
    public long Length => Content.LongLength;
}

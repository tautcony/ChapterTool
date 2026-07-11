using ChapterTool.Core.Models;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Builds deterministic chapter output file names and allocates non-colliding paths.
/// </summary>
public static class ChapterSavePath
{
    /// <summary>
    /// Builds the base output file name from the chapter set and optional source path.
    /// Disc formats append <c>__{clip}</c> when the source path base name differs from the clip name.
    /// </summary>
    /// <param name="info">The chapter set being exported.</param>
    /// <param name="sourcePath">The loaded source path, when available.</param>
    /// <returns>A file name without extension.</returns>
    public static string BuildBaseFileName(ChapterSet info, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(info);

        var fromSourcePath = FileNameWithoutExtensionOrNull(sourcePath);
        var fromSourceName = FileNameWithoutExtensionOrNull(info.SourceName);
        var baseName = fromSourcePath ?? fromSourceName ?? "chapters";

        if (info.ImportFormat is ChapterImportFormat.Mpls or ChapterImportFormat.DvdIfo
            && !string.IsNullOrWhiteSpace(fromSourceName)
            && !string.Equals(baseName, fromSourceName, StringComparison.OrdinalIgnoreCase)
            && !baseName.EndsWith("__" + fromSourceName, StringComparison.OrdinalIgnoreCase))
        {
            baseName = $"{baseName}__{fromSourceName}";
        }

        return baseName;
    }

    /// <summary>
    /// Allocates a path under <paramref name="directory"/> that does not already exist,
    /// using <c>_1</c>, <c>_2</c>, … suffixes when needed.
    /// </summary>
    /// <param name="directory">The target directory.</param>
    /// <param name="baseFileName">The base file name without extension.</param>
    /// <param name="extension">The file extension, with or without a leading dot.</param>
    /// <returns>A unique absolute or relative path under the directory.</returns>
    public static string AllocateUniqueFilePath(string directory, string baseFileName, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFileName);

        var normalizedExtension = NormalizeExtension(extension);
        var candidate = Path.Combine(directory, baseFileName + normalizedExtension);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var index = 1; index < 10_000; index++)
        {
            candidate = Path.Combine(directory, $"{baseFileName}_{index}{normalizedExtension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not allocate a unique output path under '{directory}'.");
    }

    /// <summary>
    /// Attempts to normalize a directory path to a full path.
    /// </summary>
    /// <param name="directory">The candidate directory.</param>
    /// <param name="fullPath">The normalized full path when successful.</param>
    /// <returns><see langword="true"/> when normalization succeeds.</returns>
    public static bool TryNormalizeDirectory(string? directory, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            var trimmed = directory.Trim();
            var normalized = Path.GetFullPath(trimmed);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            fullPath = normalized;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the directory that contains a loaded source file or folder path.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <returns>The containing directory, or <see langword="null"/> when it cannot be resolved.</returns>
    public static string? DirectoryOfSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(sourcePath.Trim());
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }

            var directory = Path.GetDirectoryName(fullPath);
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? FileNameWithoutExtensionOrNull(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(pathOrName.Trim());
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
    }
}

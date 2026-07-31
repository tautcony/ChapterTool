namespace ChapterTool.Infrastructure.Importing.Bdmv;

internal sealed record BdmvSourceLayout(
    string OriginalInputPath,
    string DiscRoot,
    string BdmvDirectory,
    string PrimaryIndexPath,
    string BackupIndexPath,
    string PrimaryMovieObjectPath,
    string BackupMovieObjectPath,
    string PrimaryBdjoDirectory,
    string BackupBdjoDirectory,
    string PrimaryPlaylistDirectory,
    string BackupPlaylistDirectory,
    string ClipInfoDirectory,
    string StreamDirectory,
    string MetadataDirectory)
{
    internal static BdmvSourceLayout? TryResolve(string inputPath, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "BDMV input path is empty.";
            return null;
        }

        var fullPath = Path.GetFullPath(inputPath);
        string? discRoot = null;
        if (Directory.Exists(fullPath))
        {
            if (Directory.Exists(Path.Combine(fullPath, "BDMV", "PLAYLIST")) ||
                Directory.Exists(Path.Combine(fullPath, "BDMV", "BACKUP", "PLAYLIST")))
            {
                discRoot = fullPath;
            }
            else if (string.Equals(Path.GetFileName(fullPath), "BDMV", StringComparison.OrdinalIgnoreCase) &&
                     (Directory.Exists(Path.Combine(fullPath, "PLAYLIST")) ||
                      Directory.Exists(Path.Combine(fullPath, "BACKUP", "PLAYLIST"))))
            {
                discRoot = Directory.GetParent(fullPath)?.FullName;
            }
        }
        else if (File.Exists(fullPath))
        {
            if (!string.Equals(Path.GetFileName(fullPath), "index.bdmv", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only the primary BDMV/index.bdmv file is accepted as a direct BDMV file input.";
                return null;
            }

            var bdmvDirectory = Directory.GetParent(fullPath);
            if (bdmvDirectory != null && string.Equals(bdmvDirectory.Name, "BDMV", StringComparison.OrdinalIgnoreCase) &&
                (Directory.Exists(Path.Combine(bdmvDirectory.FullName, "PLAYLIST")) ||
                 Directory.Exists(Path.Combine(bdmvDirectory.FullName, "BACKUP", "PLAYLIST"))))
            {
                discRoot = bdmvDirectory.Parent?.FullName;
            }
        }

        if (discRoot == null)
        {
            error = "The input does not identify a disc root, a BDMV directory with PLAYLIST, or BDMV/index.bdmv.";
            return null;
        }

        var bdmv = Path.Combine(discRoot, "BDMV");
        return new BdmvSourceLayout(
            fullPath,
            discRoot,
            bdmv,
            Path.Combine(bdmv, "index.bdmv"),
            Path.Combine(bdmv, "BACKUP", "index.bdmv"),
            Path.Combine(bdmv, "MovieObject.bdmv"),
            Path.Combine(bdmv, "BACKUP", "MovieObject.bdmv"),
            Path.Combine(bdmv, "BDJO"),
            Path.Combine(bdmv, "BACKUP", "BDJO"),
            Path.Combine(bdmv, "PLAYLIST"),
            Path.Combine(bdmv, "BACKUP", "PLAYLIST"),
            Path.Combine(bdmv, "CLIPINF"),
            Path.Combine(bdmv, "STREAM"),
            Path.Combine(bdmv, "META", "DL"));
    }
}

namespace ChapterTool.Infrastructure.Importing.Dvd;

internal static class DvdDiscDirectoryDetector
{
    internal static bool IsDiscDirectory(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !Directory.Exists(inputPath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(inputPath);
        return ContainsVideoManager(fullPath)
            || ContainsVideoManager(Path.Combine(fullPath, "VIDEO_TS"));
    }

    private static bool ContainsVideoManager(string directory) =>
        File.Exists(Path.Combine(directory, "VIDEO_TS.IFO"))
        || File.Exists(Path.Combine(directory, "VIDEO_TS.BUP"));
}

namespace ChapterTool.Infrastructure.Importing.Dvd;

internal sealed record DvdSourceLayout(string OriginalInputPath, string DiscRoot, string VideoTsDirectory, string VmgIfoPath)
{
    internal static DvdSourceLayout? TryResolve(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(inputPath);
        if (File.Exists(fullPath))
        {
            var fileName = Path.GetFileName(fullPath);
            if (!string.Equals(fileName, "VIDEO_TS.IFO", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fileName, "VIDEO_TS.BUP", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var videoTsDirectory = Path.GetDirectoryName(fullPath);
            if (videoTsDirectory is null)
            {
                return null;
            }

            var parent = Directory.GetParent(videoTsDirectory);
            var discRoot = string.Equals(Path.GetFileName(videoTsDirectory), "VIDEO_TS", StringComparison.OrdinalIgnoreCase)
                ? parent?.FullName ?? videoTsDirectory
                : videoTsDirectory;
            return new DvdSourceLayout(fullPath, discRoot, videoTsDirectory, fullPath);
        }

        if (!Directory.Exists(fullPath))
        {
            return null;
        }

        var directIfo = Path.Combine(fullPath, "VIDEO_TS.IFO");
        var directBackup = Path.Combine(fullPath, "VIDEO_TS.BUP");
        var selectedDirect = File.Exists(directIfo) ? directIfo : File.Exists(directBackup) ? directBackup : null;
        if (selectedDirect is not null)
        {
            var parent = Directory.GetParent(fullPath);
            var root = string.Equals(Path.GetFileName(fullPath), "VIDEO_TS", StringComparison.OrdinalIgnoreCase)
                ? parent?.FullName ?? fullPath
                : fullPath;
            return new DvdSourceLayout(fullPath, root, fullPath, selectedDirect);
        }

        var nestedVideoTsDirectory = Path.Combine(fullPath, "VIDEO_TS");
        var nestedIfo = Path.Combine(nestedVideoTsDirectory, "VIDEO_TS.IFO");
        var nestedBackup = Path.Combine(nestedVideoTsDirectory, "VIDEO_TS.BUP");
        var selectedNested = File.Exists(nestedIfo) ? nestedIfo : File.Exists(nestedBackup) ? nestedBackup : null;
        return selectedNested is not null
            ? new DvdSourceLayout(fullPath, fullPath, nestedVideoTsDirectory, selectedNested)
            : null;
    }
}

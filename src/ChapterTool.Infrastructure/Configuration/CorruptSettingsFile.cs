namespace ChapterTool.Infrastructure.Configuration;

internal static class CorruptSettingsFile
{
    public static CorruptSettingsFileException Preserve(string path, Exception exception)
    {
        var backupPath = NextBackupPath(path);
        File.Move(path, backupPath);
        return new CorruptSettingsFileException(path, backupPath, exception);
    }

    private static string NextBackupPath(string path)
    {
        var backupPath = path + ".corrupt";
        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"{backupPath}.{index}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Unable to allocate a corrupt settings backup path for '{path}'.");
    }
}

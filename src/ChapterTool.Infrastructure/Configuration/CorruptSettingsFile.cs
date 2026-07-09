using System.Runtime.InteropServices;

namespace ChapterTool.Infrastructure.Configuration;

internal static class CorruptSettingsFile
{
    private static readonly Lock SyncRoot = new();
    private static readonly Dictionary<string, LoadState> LoadStates = new(StringComparer.Ordinal);

    public static IDisposable EnterLoad(string path)
    {
        var key = NormalizePath(path);

        lock (SyncRoot)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(LoadStates, key, out _);
            state.ActiveLoads++;
        }

        return new LoadScope(key);
    }

    public static CorruptSettingsFileException Preserve(string path, Exception exception)
    {
        var key = NormalizePath(path);

        lock (SyncRoot)
        {
            if (!File.Exists(path))
            {
                var existingBackupPath = ExistingBackupPath(path);
                MarkConcurrentPreservation(key, existingBackupPath);
                return new CorruptSettingsFileException(path, existingBackupPath, exception);
            }

            var backupPath = NextBackupPath(path);
            File.Move(path, backupPath);
            MarkConcurrentPreservation(key, backupPath);
            return new CorruptSettingsFileException(path, backupPath, exception);
        }
    }

    public static bool TryGetConcurrentPreservation(string path, Exception exception, out CorruptSettingsFileException corruptException)
    {
        var key = NormalizePath(path);

        lock (SyncRoot)
        {
            if (LoadStates.TryGetValue(key, out var state)
                && state.PendingConcurrentFailures > 0
                && !string.IsNullOrEmpty(state.BackupPath))
            {
                state.PendingConcurrentFailures--;
                LoadStates[key] = state;
                corruptException = new CorruptSettingsFileException(path, state.BackupPath, exception);
                return true;
            }
        }

        corruptException = null!;
        return false;
    }

    private static void MarkConcurrentPreservation(string key, string backupPath)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(LoadStates, key, out _);
        state.BackupPath = backupPath;
        state.PendingConcurrentFailures = Math.Max(state.PendingConcurrentFailures, state.ActiveLoads - 1);
    }

    private static string ExistingBackupPath(string path)
    {
        var backupPath = path + ".corrupt";
        if (File.Exists(backupPath))
        {
            return backupPath;
        }

        var latest = Directory
            .EnumerateFiles(Path.GetDirectoryName(path) ?? ".", Path.GetFileName(backupPath) + "*")
            .Order(StringComparer.Ordinal)
            .LastOrDefault();
        return latest ?? backupPath;
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

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }

    private sealed class LoadScope(string key) : IDisposable
    {
        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (!LoadStates.TryGetValue(key, out var state))
                {
                    return;
                }

                state.ActiveLoads--;
                if (state.ActiveLoads <= 0)
                {
                    LoadStates.Remove(key);
                    return;
                }

                LoadStates[key] = state;
            }
        }
    }

    private struct LoadState
    {
        public int ActiveLoads;
        public int PendingConcurrentFailures;
        public string? BackupPath;
    }
}

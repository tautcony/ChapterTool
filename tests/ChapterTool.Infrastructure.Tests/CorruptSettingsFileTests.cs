using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class CorruptSettingsFileTests
{
    [Fact]
    public void PreserveWithMissingFileReusesExistingBackup()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var backup = path + ".corrupt";
        File.WriteAllText(backup, "old");

        var exception = CorruptSettingsFile.Preserve(path, new InvalidDataException("boom"));

        Assert.Equal(path, exception.SettingsPath);
        Assert.Equal(backup, exception.BackupPath);
        Assert.Equal("boom", exception.InnerException?.Message);
    }

    [Fact]
    public void PreserveWithMissingFileFallsBackToLatestNumberedBackup()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path + ".corrupt.7", "old");

        var exception = CorruptSettingsFile.Preserve(path, new InvalidDataException("boom"));

        Assert.Equal(path + ".corrupt.7", exception.BackupPath);
    }

    [Fact]
    public void PreserveMovesCorruptFileToNextNumberedBackup()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "corrupt");
        File.WriteAllText(path + ".corrupt", "old1");
        File.WriteAllText(path + ".corrupt.1", "old2");

        var exception = CorruptSettingsFile.Preserve(path, new InvalidDataException("boom"));

        Assert.Equal(path + ".corrupt.2", exception.BackupPath);
        Assert.True(File.Exists(path + ".corrupt.2"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void PreserveReportsUnavailableBackupWhenMoveFails()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "corrupt");
        Directory.CreateDirectory(path + ".corrupt");

        var exception = CorruptSettingsFile.Preserve(path, new InvalidDataException("boom"));

        Assert.Equal(path + ".corrupt (unavailable)", exception.BackupPath);
        Assert.IsType<AggregateException>(exception.InnerException);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ConcurrentLoadsShareOnePreservedBackup()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "corrupt");

        var first = CorruptSettingsFile.EnterLoad(path);
        var second = CorruptSettingsFile.EnterLoad(path);

        var exception = CorruptSettingsFile.Preserve(path, new InvalidDataException("boom"));
        Assert.True(File.Exists(exception.BackupPath));

        Assert.True(
            CorruptSettingsFile.TryGetConcurrentPreservation(path, new InvalidDataException("boom2"), out var concurrent));
        Assert.Equal(exception.BackupPath, concurrent.BackupPath);
        Assert.Equal("boom2", concurrent.InnerException?.Message);

        Assert.False(
            CorruptSettingsFile.TryGetConcurrentPreservation(path, new InvalidDataException("boom3"), out _));

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void TryGetConcurrentPreservationWithoutStateReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool", Guid.NewGuid().ToString("N"), "settings.json");

        Assert.False(
            CorruptSettingsFile.TryGetConcurrentPreservation(path, new InvalidDataException("boom"), out var exception));
        Assert.Null(exception);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ChapterTool_CorruptSettings_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}

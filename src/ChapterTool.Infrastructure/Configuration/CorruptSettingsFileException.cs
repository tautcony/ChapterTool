namespace ChapterTool.Infrastructure.Configuration;

public sealed class CorruptSettingsFileException : Exception
{
    public CorruptSettingsFileException(string settingsPath, string backupPath, Exception innerException)
        : base($"Settings file '{settingsPath}' contains invalid JSON. The corrupt file was preserved at '{backupPath}'.", innerException)
    {
        SettingsPath = settingsPath;
        BackupPath = backupPath;
    }

    public string SettingsPath { get; }

    public string BackupPath { get; }
}

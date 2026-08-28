namespace ChapterTool.Contracts.Configuration;

public sealed class CorruptSettingsFileException(string settingsPath, string backupPath, Exception innerException)
    : Exception(
        $"Settings file '{settingsPath}' contains invalid JSON. The corrupt file was preserved at '{backupPath}'.",
        innerException)
{
    public string SettingsPath { get; } = settingsPath;

    public string BackupPath { get; } = backupPath;
}

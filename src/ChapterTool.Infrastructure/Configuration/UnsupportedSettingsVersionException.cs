namespace ChapterTool.Infrastructure.Configuration;

public sealed class UnsupportedSettingsVersionException : IOException
{
    public UnsupportedSettingsVersionException(string settingsPath, int foundVersion, int supportedVersion)
        : base($"Settings file '{settingsPath}' uses schema version {foundVersion}, but this application supports up to version {supportedVersion}.")
    {
        SettingsPath = settingsPath;
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }

    public string SettingsPath { get; }

    public int FoundVersion { get; }

    public int SupportedVersion { get; }
}

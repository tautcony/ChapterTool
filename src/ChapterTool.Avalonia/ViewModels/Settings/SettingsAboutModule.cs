namespace ChapterTool.Avalonia.ViewModels.Settings;

/// <summary>
/// Ownership module for about/runtime display strings (version, settings directory).
/// </summary>
public sealed class SettingsAboutModule
{
    public string VersionText { get; set; } = string.Empty;
    public string SettingsDirectoryDisplay { get; set; } = string.Empty;
}

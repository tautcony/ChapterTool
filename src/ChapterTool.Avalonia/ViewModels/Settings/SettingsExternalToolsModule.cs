namespace ChapterTool.Avalonia.ViewModels.Settings;

/// <summary>
/// Ownership module for external tool paths and validation status.
/// UI still binds through <see cref="SettingsToolViewModel"/>; this type isolates path/status state.
/// </summary>
public sealed class SettingsExternalToolsModule
{
    public string MkvToolnixPath { get; set; } = string.Empty;
    public string MkvToolnixStatus { get; set; } = string.Empty;
    public string Eac3toPath { get; set; } = string.Empty;
    public string Eac3toStatus { get; set; } = string.Empty;
    public string FfprobePath { get; set; } = string.Empty;
    public string FfprobeStatus { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;
    public string FfmpegStatus { get; set; } = string.Empty;
}

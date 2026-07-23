namespace ChapterTool.Wasm.Services;

/// <summary>
/// One editable chapter grid row, matching Avalonia's chapter table columns.
/// </summary>
public sealed class ChapterRowModel
{
    public int Number { get; set; }

    public string TimeText { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FramesInfo { get; set; } = string.Empty;

    public bool IsSeparator { get; set; }

    public bool IsFrameAccurate { get; set; }

    public bool IsFrameInexact { get; set; }

    public bool IsFrameNeutral => !IsFrameAccurate && !IsFrameInexact;
}

/// <summary>
/// Clip/entry option for multi-playlist imports (Avalonia clip combo).
/// </summary>
public sealed record ClipOption(string Id, string DisplayText, int GroupIndex, int EntryIndex);

/// <summary>
/// Result of a save/export operation ready for browser download.
/// </summary>
public sealed record SaveResult(bool Success, string Message, string? Content = null, string? FileName = null);

/// <summary>
/// Result of a preview operation using the same projection/export path as Save.
/// </summary>
public sealed record PreviewResult(bool Success, string Message, string Content = "", string? FileName = null);

public sealed record WasmLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Details = null);

public sealed record WasmSettings(
    int SchemaVersion = 1,
    WasmApplicationSettings? Application = null,
    WasmThemeSettings? Theme = null,
    WasmFontSettings? Font = null);

public sealed record WasmApplicationSettings(
    string? SavingPath = null,
    string Language = "",
    WasmWindowLocation? MainWindowLocation = null,
    string? MkvToolnixPath = null,
    string? Eac3toPath = null,
    string? FfprobePath = null,
    string? FfmpegPath = null,
    string DefaultSaveFormat = "Txt",
    string DefaultXmlLanguage = "und",
    string OutputTextEncoding = "utf8",
    bool EmitBom = true,
    decimal FrameAccuracyTolerance = 0.15m);

public sealed record WasmWindowLocation(int X, int Y);

public sealed record WasmThemeSettings(string PresetId = "avalonia-default");

public sealed record WasmFontSettings(
    string UiFontFamily = "",
    string MonospaceFontFamily = "");

public sealed record WasmThemeOption(
    string Id,
    string DisplayName,
    IReadOnlyList<string> PreviewSwatches);

public sealed record WasmLanguageOption(string Code, string DisplayName);

public sealed record WasmToolOption(string Id, string Name);

public sealed record WasmFontOption(string Value, string Name);

/// <summary>
/// Browser-safe related media reference for the current clip.
/// </summary>
public sealed record RelatedMediaItem(string DisplayName, string RelativePath, string? AbsolutePath);

/// <summary>
/// Last successfully loaded source retained for Reload.
/// </summary>
public sealed record LoadedSourceSnapshot(string FileName, byte[] Content);

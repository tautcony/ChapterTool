namespace ChapterTool.Core.WasmDemo.Services;

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
/// Save-format option for the bottom format combo.
/// </summary>
public sealed record SaveFormatOption(int Index, string Code, string DisplayName, string Extension);

/// <summary>
/// Result of a save/export operation ready for browser download.
/// </summary>
public sealed record SaveResult(bool Success, string Message, string? Content = null, string? FileName = null);

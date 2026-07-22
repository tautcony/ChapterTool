namespace ChapterTool.Core.Exporting;

/// <summary>
/// Describes one export format for a host-facing format selector.
/// </summary>
public sealed record SaveFormatOption(int Index, string Code, string DisplayName, string Extension);

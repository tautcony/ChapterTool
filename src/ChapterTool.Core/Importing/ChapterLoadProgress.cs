namespace ChapterTool.Core.Importing;

/// <summary>
/// Reports chapter loading progress.
/// </summary>
/// <param name="Value">The current progress value, typically from 0 to 1.</param>
/// <param name="Message">The optional progress message shown to users.</param>
public sealed record ChapterLoadProgress(double Value, string? Message = null);

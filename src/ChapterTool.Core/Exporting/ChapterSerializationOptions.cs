namespace ChapterTool.Core.Exporting;

/// <summary>Format-only settings for serializing a captured chapter document.</summary>
public sealed record ChapterSerializationOptions(
    ChapterExportFormat Format,
    string? XmlLanguage = null,
    string? SourceFileName = null,
    OutputTextEncoding TextEncoding = OutputTextEncoding.Utf8);

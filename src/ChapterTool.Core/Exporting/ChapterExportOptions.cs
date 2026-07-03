namespace ChapterTool.Core.Exporting;

public sealed record ChapterExportOptions(
    ChapterExportFormat Format,
    string? XmlLanguage = null,
    string? SourceFileName = null,
    bool AutoGenerateNames = false,
    bool UseTemplateNames = false,
    string ChapterNameTemplateText = "",
    int OrderShift = 0,
    bool ApplyExpression = false,
    string Expression = "t",
    bool EmitBom = true,
    bool ProjectOutput = true);

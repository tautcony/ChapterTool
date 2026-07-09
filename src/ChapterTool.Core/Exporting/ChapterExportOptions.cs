namespace ChapterTool.Core.Exporting;

/// <summary>
/// Describes options for chapter export and output projection.
/// </summary>
/// <param name="Format">The target chapter export format.</param>
/// <param name="XmlLanguage">The Matroska XML chapter language code, when exporting XML.</param>
/// <param name="SourceFileName">The source file name used by formats that include source metadata.</param>
/// <param name="AutoGenerateNames">Whether to generate sequential chapter names during export.</param>
/// <param name="UseTemplateNames">Whether to apply names from <paramref name="ChapterNameTemplateText"/>.</param>
/// <param name="ChapterNameTemplateText">Newline-delimited chapter names used as a naming template.</param>
/// <param name="OrderShift">The amount added to exported chapter display numbers.</param>
/// <param name="ApplyExpression">Whether to apply <paramref name="Expression"/> before exporting.</param>
/// <param name="Expression">The Lua expression or time expression used to project chapter times.</param>
/// <param name="LuaExpressionPresetId">The selected Lua expression preset identifier, when one is used.</param>
/// <param name="LuaExpressionSourceName">The display name of the expression source, when available.</param>
/// <param name="EmitBom">Whether exported text should include a UTF-8 byte order mark.</param>
/// <param name="ProjectOutput">Whether export should apply output projection before formatting content.</param>
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
    string LuaExpressionPresetId = "",
    string LuaExpressionSourceName = "",
    bool EmitBom = true,
    bool ProjectOutput = true);

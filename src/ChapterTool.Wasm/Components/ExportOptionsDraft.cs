namespace ChapterTool.Wasm.Components;

public sealed record ExportOptionsDraft(
    int OrderShift,
    string XmlLanguage,
    int EncodingIndex,
    bool EmitBom,
    string? TemplateName,
    string? TemplateText);

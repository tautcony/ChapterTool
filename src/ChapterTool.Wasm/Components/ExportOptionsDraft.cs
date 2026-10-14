namespace ChapterTool.Wasm.Components;

public sealed record ExportOptionsDraft(
    string XmlLanguage,
    int EncodingIndex,
    bool EmitBom)
{
    public bool IsValid(IReadOnlyList<string> xmlLanguages, int encodingCount, bool xmlLanguageEnabled) =>
        EncodingIndex >= 0
        && EncodingIndex < encodingCount
        && (!xmlLanguageEnabled || xmlLanguages.Contains(XmlLanguage, StringComparer.OrdinalIgnoreCase));
}

using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.Session;

/// <summary>
/// Workspace-owned export preference snapshot: save format, XML language,
/// text encoding, BOM emission, and configured save directory.
/// </summary>
public sealed class ExportPreferences
{
    public ChapterExportFormat Format { get; private set; } = ChapterExportFormat.Txt;

    public string XmlLanguage { get; private set; } = "und";

    public OutputTextEncoding TextEncoding { get; private set; } = OutputTextEncoding.Utf8;

    public bool EmitBom { get; private set; } = true;

    /// <summary>Configured save directory from settings (null means unresolved / source-relative).</summary>
    public string? SaveDirectory { get; private set; }

    public bool SetFormat(ChapterExportFormat value)
    {
        if (Format == value)
        {
            return false;
        }

        Format = value;
        return true;
    }

    public bool SetXmlLanguage(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "und" : value.Trim().ToLowerInvariant();
        if (string.Equals(XmlLanguage, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        XmlLanguage = normalized;
        return true;
    }

    public bool SetTextEncoding(OutputTextEncoding value)
    {
        if (TextEncoding == value)
        {
            return false;
        }

        TextEncoding = value;
        return true;
    }

    public bool SetEmitBom(bool value)
    {
        if (EmitBom == value)
        {
            return false;
        }

        EmitBom = value;
        return true;
    }

    public bool SetSaveDirectory(string? value)
    {
        if (string.Equals(SaveDirectory, value, StringComparison.Ordinal))
        {
            return false;
        }

        SaveDirectory = value;
        return true;
    }
}

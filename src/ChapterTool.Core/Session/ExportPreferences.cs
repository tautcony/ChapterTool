using ChapterTool.Core.Exporting;

namespace ChapterTool.Core.Session;

/// <summary>
/// Workspace-owned export preference snapshot: save format, XML language,
/// text encoding, BOM emission, and configured save directory.
/// </summary>
public sealed class ExportPreferences
{
    /// <summary>Gets the selected export format.</summary>
    public ChapterExportFormat Format { get; private set; } = ChapterExportFormat.Txt;

    /// <summary>Gets the XML language code used for Matroska XML export.</summary>
    public string XmlLanguage { get; private set; } = "und";

    /// <summary>Gets the text encoding used for text exports.</summary>
    public OutputTextEncoding TextEncoding { get; private set; } = OutputTextEncoding.Utf8;

    /// <summary>Gets a value indicating whether a UTF BOM is emitted for text exports.</summary>
    public bool EmitBom { get; private set; }

    /// <summary>Configured save directory from settings (null means unresolved / source-relative).</summary>
    public string? SaveDirectory { get; private set; }

    /// <summary>Sets the export format. Returns whether the value changed.</summary>
    /// <param name="value">The new format.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetFormat(ChapterExportFormat value)
    {
        if (Format == value)
        {
            return false;
        }

        Format = value;
        return true;
    }

    /// <summary>Sets the XML language code. Returns whether the value changed.</summary>
    /// <param name="value">The new language code.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
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

    /// <summary>Sets the text encoding. Returns whether the value changed.</summary>
    /// <param name="value">The new encoding.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetTextEncoding(OutputTextEncoding value)
    {
        if (TextEncoding == value)
        {
            return false;
        }

        TextEncoding = value;
        return true;
    }

    /// <summary>Sets whether a UTF BOM is emitted. Returns whether the value changed.</summary>
    /// <param name="value">The new BOM preference.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetEmitBom(bool value)
    {
        if (EmitBom == value)
        {
            return false;
        }

        EmitBom = value;
        return true;
    }

    /// <summary>Sets the configured save directory. Returns whether the value changed.</summary>
    /// <param name="value">The new directory path, or null for source-relative output.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
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

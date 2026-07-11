using System.Text;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Supported encodings for exported text files.
/// </summary>
public enum OutputTextEncoding
{
    /// <summary>UTF-8.</summary>
    Utf8,
    /// <summary>Little-endian UTF-16.</summary>
    Utf16LittleEndian,
    /// <summary>Big-endian UTF-16.</summary>
    Utf16BigEndian,
    /// <summary>Little-endian UTF-32.</summary>
    Utf32LittleEndian,
    /// <summary>Big-endian UTF-32.</summary>
    Utf32BigEndian
}

/// <summary>
/// Resolves output encoding metadata and runtime encoders.
/// </summary>
public static class OutputTextEncodings
{
    /// <summary>Gets all supported output encodings in display order.</summary>
    public static IReadOnlyList<OutputTextEncoding> All { get; } =
    [
        OutputTextEncoding.Utf8,
        OutputTextEncoding.Utf16LittleEndian,
        OutputTextEncoding.Utf16BigEndian,
        OutputTextEncoding.Utf32LittleEndian,
        OutputTextEncoding.Utf32BigEndian
    ];

    /// <summary>Creates the selected encoding with the requested BOM behavior.</summary>
    public static Encoding Create(OutputTextEncoding encoding, bool emitBom) => encoding switch
    {
        OutputTextEncoding.Utf8 => new UTF8Encoding(emitBom),
        OutputTextEncoding.Utf16LittleEndian => new UnicodeEncoding(bigEndian: false, byteOrderMark: emitBom),
        OutputTextEncoding.Utf16BigEndian => new UnicodeEncoding(bigEndian: true, byteOrderMark: emitBom),
        OutputTextEncoding.Utf32LittleEndian => new UTF32Encoding(bigEndian: false, byteOrderMark: emitBom),
        OutputTextEncoding.Utf32BigEndian => new UTF32Encoding(bigEndian: true, byteOrderMark: emitBom),
        _ => new UTF8Encoding(emitBom)
    };

    /// <summary>Returns the user-facing name of an encoding.</summary>
    public static string DisplayName(OutputTextEncoding encoding) => encoding switch
    {
        OutputTextEncoding.Utf8 => "UTF-8",
        OutputTextEncoding.Utf16LittleEndian => "UTF-16 LE",
        OutputTextEncoding.Utf16BigEndian => "UTF-16 BE",
        OutputTextEncoding.Utf32LittleEndian => "UTF-32 LE",
        OutputTextEncoding.Utf32BigEndian => "UTF-32 BE",
        _ => "UTF-8"
    };

    /// <summary>Returns the lowercase settings identifier of an encoding.</summary>
    public static string Id(OutputTextEncoding encoding) => encoding switch
    {
        OutputTextEncoding.Utf16LittleEndian => "utf16le",
        OutputTextEncoding.Utf16BigEndian => "utf16be",
        OutputTextEncoding.Utf32LittleEndian => "utf32le",
        OutputTextEncoding.Utf32BigEndian => "utf32be",
        _ => "utf8"
    };

    /// <summary>Parses a lowercase settings identifier, defaulting to UTF-8.</summary>
    public static OutputTextEncoding ParseOrDefault(string? value)
    {
        return value switch
        {
            "utf16le" => OutputTextEncoding.Utf16LittleEndian,
            "utf16be" => OutputTextEncoding.Utf16BigEndian,
            "utf32le" => OutputTextEncoding.Utf32LittleEndian,
            "utf32be" => OutputTextEncoding.Utf32BigEndian,
            _ => OutputTextEncoding.Utf8
        };
    }

    /// <summary>Returns the encoding name used in XML declarations.</summary>
    public static string XmlName(OutputTextEncoding encoding) => encoding switch
    {
        OutputTextEncoding.Utf16LittleEndian => "utf-16",
        OutputTextEncoding.Utf16BigEndian => "utf-16BE",
        OutputTextEncoding.Utf32LittleEndian => "utf-32",
        OutputTextEncoding.Utf32BigEndian => "utf-32BE",
        _ => "utf-8"
    };
}

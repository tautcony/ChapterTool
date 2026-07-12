using System.Xml;
using System.Xml.Linq;

namespace ChapterTool.Core.Importing;

/// <summary>
/// Loads untrusted XML without permitting DTD processing or external entity resolution.
/// </summary>
internal static class SecureXmlLoader
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    /// <summary>
    /// Loads an XML document with the application's secure XML policy.
    /// </summary>
    public static XmlDocument LoadXmlDocument(Stream stream)
    {
        var document = new XmlDocument
        {
            XmlResolver = null
        };
        using var reader = XmlReader.Create(stream, ReaderSettings);
        document.Load(reader);
        return document;
    }

    /// <summary>
    /// Loads XML text with the application's secure XML policy.
    /// </summary>
    public static XmlDocument LoadXmlDocument(string text)
    {
        using var textReader = new StringReader(text);
        using var reader = XmlReader.Create(textReader, ReaderSettings);
        var document = new XmlDocument
        {
            XmlResolver = null
        };
        document.Load(reader);
        return document;
    }

    /// <summary>
    /// Loads an XDocument with the application's secure XML policy.
    /// </summary>
    public static async ValueTask<XDocument> LoadXDocumentAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(stream, ReaderSettings);
        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }
}

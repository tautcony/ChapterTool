using System.Xml.Linq;

namespace ChapterTool.Infrastructure.Importing.Bdmv;

internal static class BdmvMetadataReader
{
    internal static string ReadDiscTitle(string metadataDirectory)
    {
        try
        {
            var file = Directory.Exists(metadataDirectory)
                ? Directory.EnumerateFiles(metadataDirectory, "*.xml", SearchOption.TopDirectoryOnly)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : null;
            if (file is null)
            {
                return string.Empty;
            }

            var document = XDocument.Load(file, LoadOptions.None);
            return document.Descendants()
                .FirstOrDefault(static element => element.Name.LocalName == "name")?.Value.Trim() ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException)
        {
            return string.Empty;
        }
    }
}

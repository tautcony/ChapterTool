using System.Xml;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

/// <summary>
/// Imports Matroska XML chapter documents.
/// </summary>
/// <param name="timeFormatter">The chapter time formatter.</param>
public sealed class XmlChapterImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "matroska-xml";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".xml"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            try
            {
                var document = new XmlDocument();
                document.Load(request.Content);
                request.Content.Position = 0;
                return ParseDocument(document, request.Path);
            }
            catch (XmlException exception)
            {
                return ChapterImportResult.Failed(Error("InvalidXml", exception.Message));
            }
        }

        try
        {
            var document = new XmlDocument();
            await using var stream = File.OpenRead(request.Path);
            document.Load(stream);
            return ParseDocument(document, request.Path);
        }
        catch (XmlException exception)
        {
            return ChapterImportResult.Failed(Error("InvalidXml", exception.Message));
        }
    }

    /// <summary>
    /// Imports chapters from text content.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="path">The source path.</param>
    /// <returns>The operation result.</returns>
    public ChapterImportResult ImportText(string text, string path = "")
    {
        var document = new XmlDocument();
        try
        {
            document.LoadXml(text);
        }
        catch (XmlException exception)
        {
            return ChapterImportResult.Failed(Error("InvalidXml", exception.Message));
        }

        return ParseDocument(document, path);
    }

    private ChapterImportResult ParseDocument(XmlDocument document, string path)
    {
        var root = document.DocumentElement;
        if (root is null)
        {
            return ChapterImportResult.Failed(Error("EmptyXml", "XML document has no root element."));
        }

        if (root.Name != "Chapters")
        {
            return ChapterImportResult.Failed(Error("XmlInvalidRoot", $"Expected Chapters root, got {root.Name}.",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = root.Name }));
        }

        var groups = new List<ChapterInfoGroup>();
        var options = new List<ChapterSourceOption>();
        var defaultOptionIndex = 0;
        var hasDefaultEdition = false;
        var editionIndex = 0;
        foreach (XmlNode child in root.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Comment)
            {
                continue;
            }

            if (child.Name != "EditionEntry")
            {
                return ChapterImportResult.Failed(Error("InvalidEntryElement", $"Expected EditionEntry, got {child.Name}.",
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = child.Name }));
            }

            var isDefaultEdition = false;
            var chapters = new List<Chapter>();
            var atomIndex = 0;
            foreach (XmlNode atom in child.ChildNodes)
            {
                if (atom.Name == "ChapterAtom")
                {
                    chapters.AddRange(ParseAtom(atom, ++atomIndex));
                }
                else if (atom is { Name: "EditionFlagDefault", InnerText: "1" })
                {
                    isDefaultEdition = true;
                }
            }

            for (var i = 0; i < chapters.Count - 1; i++)
            {
                if (chapters[i].Time == chapters[i + 1].Time)
                {
                    chapters.RemoveAt(i--);
                }
            }

            var info = new ChapterInfo(
                $"Edition {editionIndex + 1:D2}",
                Path.GetFileName(path),
                editionIndex,
                "XML",
                0,
                chapters.Count == 0 ? TimeSpan.Zero : chapters[^1].Time,
                Renumber(chapters));
            options.Add(new ChapterSourceOption($"edition-{editionIndex}", info.Title, info));

            if (isDefaultEdition && !hasDefaultEdition)
            {
                defaultOptionIndex = editionIndex;
                hasDefaultEdition = true;
            }

            editionIndex++;
        }

        if (options.Count == 0 || options.All(static option => option.ChapterInfo.Chapters.Count == 0))
        {
            return ChapterImportResult.Failed(Error("XmlNoChapters", "No Matroska XML chapters were parsed."));
        }

        groups.Add(new ChapterInfoGroup(path, options, defaultOptionIndex));
        return new ChapterImportResult(true, groups, []);
    }

    private IEnumerable<Chapter> ParseAtom(XmlNode atom, int index)
    {
        var start = TimeSpan.Zero;
        TimeSpan? end = null;
        var name = string.Empty;
        var hasStart = false;
        var inner = new List<Chapter>();

        foreach (XmlNode child in atom.ChildNodes)
        {
            switch (child.Name)
            {
                case "ChapterTimeStart":
                    start = timeFormatter.ParseOrZero(child.InnerText);
                    hasStart = true;
                    break;
                case "ChapterTimeEnd":
                    end = timeFormatter.ParseOrZero(child.InnerText);
                    break;
                case "ChapterDisplay":
                    name = child.ChildNodes.Cast<XmlNode>().FirstOrDefault(static node => node.Name == "ChapterString")?.InnerText ?? string.Empty;
                    break;
                case "ChapterAtom":
                    inner.AddRange(ParseAtom(child, index));
                    break;
            }
        }

        if (hasStart)
        {
            yield return new Chapter(index, start, name, End: end);
        }

        foreach (var chapter in inner)
        {
            yield return chapter;
        }

    }

    private static IReadOnlyList<Chapter> Renumber(IReadOnlyList<Chapter> chapters) =>
        chapters.Select((chapter, index) => chapter with { Number = index + 1 }).ToList();

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private static ChapterDiagnostic Error(string code, string message, IReadOnlyDictionary<string, object?> arguments) =>
        new(DiagnosticSeverity.Error, code, message, Arguments: arguments);
}

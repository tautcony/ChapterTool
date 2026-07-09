using System.Globalization;
using System.Xml.Linq;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

/// <summary>
/// Imports HD DVD chapter data from XPL playlist files.
/// </summary>
public sealed class XplChapterImporter : IChapterImporter
{
    private static readonly XNamespace Namespace = "http://www.dvdforum.org/2005/HDDVDVideo/Playlist";

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "hddvd-xpl";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".xpl"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            XDocument document;
            if (request.Content is null)
            {
                await using var file = File.OpenRead(request.Path);
                document = await XDocument.LoadAsync(file, LoadOptions.None, cancellationToken);
            }
            else
            {
                document = await XDocument.LoadAsync(request.Content, LoadOptions.None, cancellationToken);
            }

            var entries = Parse(document, request.Path).ToList();
            if (entries.Count == 0)
            {
                return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.XplNoChapters, "No HD-DVD chapters were parsed."));
            }

            return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], []);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or InvalidOperationException or System.Xml.XmlException)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.XplParseFailed, exception.Message));
        }
    }

    private static IEnumerable<ChapterImportEntry> Parse(XDocument document, string path)
    {
        var playlist = document.Element(Namespace + "Playlist") ?? throw new InvalidDataException("Missing XPL Playlist root.");
        var optionIndex = 0;
        foreach (var titleSet in playlist.Elements(Namespace + "TitleSet"))
        {
            var timeBase = ParseFps((string?)titleSet.Attribute("timeBase")) ?? 60;
            var tickBase = ParseFps((string?)titleSet.Attribute("tickBase")) ?? 24;
            foreach (var title in titleSet.Elements(Namespace + "Title").Where(static title => title.Element(Namespace + "ChapterList") is not null))
            {
                var tickBaseDivisor = (int?)title.Attribute("tickBaseDivisor") ?? 1;
                var titleName = Path.GetFileNameWithoutExtension(path);
                titleName = (string?)title.Attribute("id") ?? titleName;
                titleName = (string?)title.Attribute("displayName") ?? titleName;
                var durationText = (string?)title.Attribute("titleDuration") ?? throw new InvalidDataException("Missing titleDuration.");
                var chapters = title.Element(Namespace + "ChapterList")!
                    .Elements(Namespace + "Chapter")
                    .Select((chapter, index) =>
                    {
                        var name = (string?)chapter.Attribute("displayName") ?? (string?)chapter.Attribute("id") ?? string.Empty;
                        var timeText = (string?)chapter.Attribute("titleTimeBegin") ?? throw new InvalidDataException("Missing titleTimeBegin.");
                        return new Chapter(index + 1, ParseTime(timeText, timeBase, tickBase, tickBaseDivisor), name);
                    })
                    .ToList();
                if (chapters.Count == 0)
                {
                    continue;
                }

                var sourceName = (string?)title.Element(Namespace + "PrimaryAudioVideoClip")?.Attribute("src") ?? string.Empty;
                var info = new ChapterSet(
                    titleName,
                    sourceName,
                    ChapterImportFormat.HdDvdXpl,
                    24,
                    ParseTime(durationText, timeBase, tickBase, tickBaseDivisor),
                    chapters);
                IReadOnlyList<ReferencedMediaFile> mediaReferences = string.IsNullOrWhiteSpace(sourceName)
                    ? []
                    : [new ReferencedMediaFile(Path.GetFileName(sourceName), Path.Combine("..", "HVDVD_TS", Path.GetFileName(sourceName)))];
                yield return new ChapterImportEntry($"title-{optionIndex}", $"{info.Title}__{chapters.Count}", info, ReferencedMediaFiles: mediaReferences);
                optionIndex++;
            }
        }
    }

    private static double? ParseFps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.Parse(value.Replace("fps", string.Empty, StringComparison.OrdinalIgnoreCase), CultureInfo.InvariantCulture);
    }

    private static TimeSpan ParseTime(string value, double timeBase, double tickBase, int tickBaseDivisor)
    {
        var colon = value.LastIndexOf(':');
        if (colon <= 0)
        {
            throw new FormatException($"Invalid HD-DVD time: {value}");
        }

        var main = TimeSpan.Parse(value[..colon], CultureInfo.InvariantCulture);
        main = TimeSpan.FromSeconds(main.TotalSeconds / 60D * timeBase);
        var tickDuration = TimeSpan.TicksPerSecond / ((decimal)tickBase / tickBaseDivisor);
        var ticks = decimal.Parse(value[(colon + 1)..], CultureInfo.InvariantCulture) * tickDuration;
        return main.Add(TimeSpan.FromTicks((long)ticks));
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}

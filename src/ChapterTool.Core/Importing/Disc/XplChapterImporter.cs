using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

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
                document = await SecureXmlLoader.LoadXDocumentAsync(file, cancellationToken);
            }
            else
            {
                document = await SecureXmlLoader.LoadXDocumentAsync(request.Content, cancellationToken);
            }

            var entries = Parse(document, request.Path).ToList();
            if (entries.Count == 0)
            {
                return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.XplNoChapters, "No HD-DVD chapters were parsed."));
            }

            return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], []);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or InvalidOperationException or System.Xml.XmlException or OverflowException or DivideByZeroException)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.XplParseFailed, exception.Message));
        }
    }

    private static IEnumerable<ChapterImportEntry> Parse(XDocument document, string path)
    {
        var playlist = document.Element(Namespace + "Playlist") ?? throw new InvalidDataException("Missing XPL Playlist root.");
        var defaultTitleName = Path.GetFileNameWithoutExtension(path);
        var optionIndex = 0;
        foreach (var titleSet in playlist.Elements(Namespace + "TitleSet"))
        {
            var timeBase = ParseClock((string?)titleSet.Attribute("timeBase")) ?? 60;
            var tickBase = ParseClock((string?)titleSet.Attribute("tickBase")) ?? 24;
            foreach (var title in titleSet.Elements(Namespace + "Title"))
            {
                if (!TryCreateEntry(title, defaultTitleName, timeBase, tickBase, optionIndex, out var entry))
                {
                    continue;
                }

                yield return entry;
                optionIndex++;
            }
        }
    }

    private static bool TryCreateEntry(XElement title, string defaultTitleName, decimal timeBase, decimal tickBase, int optionIndex, out ChapterImportEntry entry)
    {
        entry = null!;
        if (title.Element(Namespace + "ChapterList") is not { } chapterList)
        {
            return false;
        }

        var tickBaseDivisor = (int?)title.Attribute("tickBaseDivisor") ?? 1;
        var titleName = (string?)title.Attribute("displayName") ?? (string?)title.Attribute("id") ?? defaultTitleName;
        var durationText = (string?)title.Attribute("titleDuration") ?? throw new InvalidDataException("Missing titleDuration.");
        var chapters = chapterList.Elements(Namespace + "Chapter")
            .Select((chapter, index) => CreateChapter(chapter, index, timeBase, tickBase, tickBaseDivisor))
            .ToList();
        if (chapters.Count == 0)
        {
            return false;
        }

        var sourceName = (string?)title.Element(Namespace + "PrimaryAudioVideoClip")?.Attribute("src") ?? string.Empty;
        var info = new ChapterSet(
            titleName,
            sourceName,
            ChapterImportFormat.HdDvdXpl,
            FrameRateValue.Unknown,
            ParseTime(durationText, timeBase, tickBase, tickBaseDivisor),
            chapters);
        IReadOnlyList<ReferencedMediaFile> mediaReferences = string.IsNullOrWhiteSpace(sourceName)
            ? []
            : [new ReferencedMediaFile(Path.GetFileName(sourceName), Path.Combine("..", "HVDVD_TS", Path.GetFileName(sourceName)))];
        entry = new ChapterImportEntry($"title-{optionIndex}", info.Title, info, ReferencedMediaFiles: mediaReferences);
        return true;
    }

    private static Chapter CreateChapter(XElement chapter, int index, decimal timeBase, decimal tickBase, int tickBaseDivisor)
    {
        var name = (string?)chapter.Attribute("displayName") ?? (string?)chapter.Attribute("id") ?? string.Empty;
        var timeText = (string?)chapter.Attribute("titleTimeBegin") ?? throw new InvalidDataException("Missing titleTimeBegin.");
        return new Chapter(index + 1, ParseTime(timeText, timeBase, tickBase, tickBaseDivisor), name);
    }

    private static decimal? ParseClock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Replace("fps", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || parsed <= 0)
        {
            throw new InvalidDataException($"Invalid XPL clock value: {value}");
        }

        return parsed;
    }

    private static TimeSpan ParseTime(string value, decimal timeBase, decimal tickBase, int tickBaseDivisor)
    {
        if (tickBaseDivisor <= 0)
        {
            throw new InvalidDataException($"Invalid XPL tickBaseDivisor: {tickBaseDivisor}");
        }

        var colon = value.LastIndexOf(':');
        if (colon <= 0)
        {
            throw new FormatException($"Invalid HD-DVD time: {value}");
        }

        var main = TimeSpan.Parse(value[..colon], CultureInfo.InvariantCulture);
        var titleClock = ExactFrameMath.FromDecimal(timeBase);
        var pageClock = ExactFrameMath.FromDecimal(tickBase);
        var suffix = ExactFrameMath.FromDecimal(decimal.Parse(value[(colon + 1)..], CultureInfo.InvariantCulture));

        // Preserve the title-timeline interpretation and truncate each imported component to ticks.
        var mainTicks = new BigInteger(main.Ticks) * titleClock.Numerator / (60 * titleClock.Denominator);
        var suffixTicks = suffix.Numerator * TimeSpan.TicksPerSecond * pageClock.Denominator * tickBaseDivisor
            / (suffix.Denominator * pageClock.Numerator);
        return TimeSpan.FromTicks(checked((long)(mainTicks + suffixTicks)));
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}

using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Bdmv;
using ChapterTool.Infrastructure.Importing.Matroska;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Infrastructure.Importing.Runtime;

public sealed class RuntimeChapterImporterRegistry : IChapterImporterRegistry
{
    private static readonly IReadOnlySet<string> Mp4Extensions = CreateExtensionSet(".mp4", ".m4a", ".m4v");
    private static readonly IReadOnlySet<string> MatroskaExtensions = CreateExtensionSet(".mkv", ".mka", ".mks", ".webm");
    private static readonly IReadOnlySet<string> MediaExtensions = CreateExtensionSet(
        ".mp4", ".m4a", ".m4v", ".mov", ".qt", ".3gp", ".3g2",
        ".asf", ".wmv", ".wma", ".mp3", ".aac", ".ogg", ".oga", ".ogv",
        ".opus", ".wav", ".nut", ".aa", ".aax", ".ffmetadata", ".ffmeta");

    private readonly IChapterTimeFormatter formatter;
    private readonly IExternalToolLocator toolLocator;
    private readonly BdmvImporter bdmvImporter = new();
    private readonly TextChapterImporter textImporter;
    private readonly PremiereMarkerListImporter premiereMarkerListImporter;
    private readonly XmlChapterImporter xmlImporter;
    private readonly WebVttChapterImporter webVttImporter = new();
    private readonly CueChapterImporter cueImporter = new();
    private readonly FlacCueImporter flacCueImporter = new();
    private readonly TakCueImporter takCueImporter = new();
    private readonly MplsChapterImporter mplsImporter = new();
    private readonly IfoChapterImporter ifoImporter = new();
    private readonly XplChapterImporter xplImporter = new();
    private readonly MatroskaChapterImporter matroskaImporter;
    private readonly MediaChapterImporter mediaImporter;
    private readonly MediaChapterImporter mp4FallbackImporter;
    private readonly IReadOnlyDictionary<string, IChapterImporter> importers;

    public RuntimeChapterImporterRegistry(
        IChapterTimeFormatter formatter,
        IExternalToolLocator toolLocator,
        IProcessRunner processRunner,
        IMediaChapterReader mediaChapterReader,
        IMediaChapterReader mp4FallbackChapterReader)
    {
        this.formatter = formatter;
        this.toolLocator = toolLocator;
        textImporter = new TextChapterImporter(formatter);
        premiereMarkerListImporter = new PremiereMarkerListImporter(formatter);
        xmlImporter = new XmlChapterImporter(formatter);
        matroskaImporter = new MatroskaChapterImporter(toolLocator, processRunner, formatter);
        mediaImporter = new MediaChapterImporter(mediaChapterReader);
        mp4FallbackImporter = new MediaChapterImporter(mp4FallbackChapterReader, Mp4Extensions);
        importers = CreateImporterMap();
    }

    internal IChapterTimeFormatter Formatter => formatter;

    internal IExternalToolLocator ToolLocator => toolLocator;

    public IChapterImporter? Resolve(string path)
    {
        if (BdmvSourceLayout.TryResolve(path, out _) != null)
        {
            return bdmvImporter;
        }

        return importers.TryGetValue(Path.GetExtension(path), out var importer) ? importer : null;
    }

    private IReadOnlyDictionary<string, IChapterImporter> CreateImporterMap()
    {
        var map = new Dictionary<string, IChapterImporter>(StringComparer.OrdinalIgnoreCase)
        {
            [".txt"] = textImporter,
            [".csv"] = premiereMarkerListImporter,
            [".xml"] = xmlImporter,
            [".vtt"] = webVttImporter,
            [".cue"] = cueImporter,
            [".flac"] = flacCueImporter,
            [".tak"] = takCueImporter,
            [".mpls"] = mplsImporter,
            [".ifo"] = ifoImporter,
            [".xpl"] = xplImporter,
            [".bdmv"] = bdmvImporter
        };
        RegisterImporterGroup(map, matroskaImporter, MatroskaExtensions);
        RegisterImporterGroup(map, mediaImporter, MediaExtensions);
        return map;
    }

    private static IReadOnlySet<string> CreateExtensionSet(params string[] extensions) =>
        new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

    private static void RegisterImporterGroup(
        IDictionary<string, IChapterImporter> map,
        IChapterImporter importer,
        IEnumerable<string> extensions)
    {
        foreach (var extension in extensions)
        {
            map.Add(extension, importer);
        }
    }

    public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return ResolveMp4Fallback(extension, primaryImporter, primaryResult)
            ?? ResolveMatroskaFallback(extension, primaryImporter, primaryResult)
            ?? ResolveFlacFallback(extension, primaryImporter, primaryResult);
    }

    private IChapterImporter? ResolveMp4Fallback(string extension, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        if (Mp4Extensions.Contains(extension)
            && ReferenceEquals(primaryImporter, mediaImporter)
            && HasDiagnostic(primaryResult, ChapterDiagnosticCode.FfprobeMissingDependency, ChapterDiagnosticCode.FfprobeCannotStart))
        {
            return mp4FallbackImporter;
        }

        return null;
    }

    private IChapterImporter? ResolveMatroskaFallback(string extension, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        if (MatroskaExtensions.Contains(extension)
            && primaryImporter is MatroskaChapterImporter
            && HasDiagnostic(primaryResult, ChapterDiagnosticCode.MatroskaMissingDependency, ChapterDiagnosticCode.MatroskaCannotStart))
        {
            return mediaImporter;
        }

        return null;
    }

    private IChapterImporter? ResolveFlacFallback(string extension, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        if (extension == ".flac"
            && primaryImporter is FlacCueImporter
            && HasDiagnostic(primaryResult, ChapterDiagnosticCode.FlacEmbeddedCueNotFound))
        {
            return mediaImporter;
        }

        return null;
    }

    private static bool HasDiagnostic(ChapterImportResult result, params ChapterDiagnosticCode[] codes) =>
        result.Diagnostics.Any(diagnostic => codes.Contains(diagnostic.Code));
}

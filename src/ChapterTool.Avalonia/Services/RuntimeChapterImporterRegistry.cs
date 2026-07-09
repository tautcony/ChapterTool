using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Bdmv;
using ChapterTool.Infrastructure.Importing.Matroska;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterImporterRegistry(
    IChapterTimeFormatter formatter,
    IExternalToolLocator toolLocator,
    IProcessRunner processRunner,
    IMediaChapterReader mediaChapterReader,
    IMediaChapterReader mp4FallbackChapterReader) : IChapterImporterRegistry
{
    private readonly BdmvChapterImporter bdmvImporter = new(toolLocator, processRunner, formatter);
    private readonly TextChapterImporter textImporter = new(formatter);
    private readonly PremiereMarkerListImporter premiereMarkerListImporter = new(formatter);
    private readonly XmlChapterImporter xmlImporter = new(formatter);
    private readonly WebVttChapterImporter webVttImporter = new();
    private readonly CueChapterImporter cueImporter = new();
    private readonly FlacCueImporter flacCueImporter = new();
    private readonly TakCueImporter takCueImporter = new();
    private readonly MplsChapterImporter mplsImporter = new();
    private readonly IfoChapterImporter ifoImporter = new();
    private readonly XplChapterImporter xplImporter = new();
    private readonly MatroskaChapterImporter matroskaImporter = new(toolLocator, processRunner, formatter);
    private readonly MediaChapterImporter mediaImporter = new(mediaChapterReader);
    private readonly MediaChapterImporter mp4FallbackImporter = new(mp4FallbackChapterReader, [".mp4", ".m4a", ".m4v"]);

    public IChapterImporter? Resolve(string path)
    {
        if (Directory.Exists(path) && Directory.Exists(Path.Combine(path, "BDMV", "PLAYLIST")))
        {
            return bdmvImporter;
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => textImporter,
            ".csv" => premiereMarkerListImporter,
            ".xml" => xmlImporter,
            ".vtt" => webVttImporter,
            ".cue" => cueImporter,
            ".flac" => flacCueImporter,
            ".tak" => takCueImporter,
            ".mpls" => mplsImporter,
            ".ifo" => ifoImporter,
            ".xpl" => xplImporter,
            ".mkv" or ".mka" or ".mks" or ".webm" => matroskaImporter,
            ".mp4" or ".m4a" or ".m4v" or ".mov" or ".qt" or ".3gp" or ".3g2" => mediaImporter,
            ".asf" or ".wmv" or ".wma" or ".mp3" or ".aac" or ".ogg" or ".oga" or ".ogv" or ".opus" or ".wav" or ".nut" or ".aa" or ".aax" or ".ffmetadata" or ".ffmeta" => mediaImporter,
            _ => null
        };
    }

    public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".mp4" or ".m4a" or ".m4v" when ReferenceEquals(primaryImporter, mediaImporter) && HasDiagnostic(primaryResult, ChapterDiagnosticCode.FfprobeMissingDependency, ChapterDiagnosticCode.FfprobeCannotStart)
                => mp4FallbackImporter,
            ".mkv" or ".mka" or ".mks" or ".webm" when primaryImporter is MatroskaChapterImporter && HasDiagnostic(primaryResult, ChapterDiagnosticCode.MatroskaMissingDependency, ChapterDiagnosticCode.MatroskaCannotStart)
                => mediaImporter,
            ".flac" when primaryImporter is FlacCueImporter && HasDiagnostic(primaryResult, ChapterDiagnosticCode.FlacEmbeddedCueNotFound)
                => mediaImporter,
            _ => null
        };
    }

    private static bool HasDiagnostic(ChapterImportResult result, params ChapterDiagnosticCode[] codes) =>
        result.Diagnostics.Any(diagnostic => codes.Contains(diagnostic.Code));
}

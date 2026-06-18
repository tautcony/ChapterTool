using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Bdmv;
using ChapterTool.Infrastructure.Importing.Matroska;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterImporterRegistry(
    IChapterTimeFormatter formatter,
    IExternalToolLocator toolLocator,
    IProcessRunner processRunner,
    IMediaChapterReader mediaChapterReader,
    IMp4ChapterReader mp4ChapterReader) : IChapterImporterRegistry
{
    public IChapterImporter? Resolve(string path)
    {
        if (Directory.Exists(path) && Directory.Exists(Path.Combine(path, "BDMV", "PLAYLIST")))
        {
            return new BdmvChapterImporter(toolLocator, processRunner, formatter);
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => new OgmChapterImporter(formatter),
            ".xml" => new XmlChapterImporter(formatter),
            ".vtt" => new WebVttChapterImporter(),
            ".cue" => new CueChapterImporter(),
            ".flac" => new FlacCueImporter(),
            ".tak" => new TakCueImporter(),
            ".mpls" => new MplsChapterImporter(),
            ".ifo" => new IfoChapterImporter(),
            ".xpl" => new XplChapterImporter(),
            ".mkv" or ".mka" or ".mks" or ".webm" => new MatroskaChapterImporter(toolLocator, processRunner, formatter),
            ".mp4" or ".m4a" or ".m4v" or ".mov" or ".qt" or ".3gp" or ".3g2" => new MediaChapterImporter(mediaChapterReader),
            ".asf" or ".wmv" or ".wma" or ".mp3" or ".aac" or ".ogg" or ".oga" or ".ogv" or ".opus" or ".wav" or ".nut" or ".aa" or ".aax" or ".ffmetadata" or ".ffmeta" => new MediaChapterImporter(mediaChapterReader),
            _ => null
        };
    }

    public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".mp4" or ".m4a" or ".m4v" when primaryImporter is MediaChapterImporter && HasDiagnostic(primaryResult, "FfprobeMissingDependency", "FfprobeCannotStart")
                => new Mp4ChapterImporter(mp4ChapterReader),
            ".mkv" or ".mka" or ".mks" or ".webm" when primaryImporter is MatroskaChapterImporter && HasDiagnostic(primaryResult, "MatroskaMissingDependency", "MatroskaCannotStart")
                => new MediaChapterImporter(mediaChapterReader),
            ".flac" when primaryImporter is FlacCueImporter && HasDiagnostic(primaryResult, "FlacEmbeddedCueNotFound")
                => new MediaChapterImporter(mediaChapterReader),
            _ => null
        };
    }

    private static bool HasDiagnostic(ChapterImportResult result, params string[] codes) =>
        result.Diagnostics.Any(diagnostic => codes.Contains(diagnostic.Code, StringComparer.Ordinal));
}

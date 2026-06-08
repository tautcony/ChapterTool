using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Bdmv;
using ChapterTool.Infrastructure.Importing.Matroska;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Services;

public sealed class RuntimeChapterImporterRegistry(
    IChapterTimeFormatter formatter,
    IExternalToolLocator toolLocator,
    IProcessRunner processRunner,
    INativeDependencyService nativeDependencyService) : IChapterImporterRegistry
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
            ".mkv" or ".mka" => new MatroskaChapterImporter(toolLocator, processRunner, formatter),
            ".mp4" or ".m4a" or ".m4v" => new Mp4ChapterImporter(new MissingMp4ChapterReader(nativeDependencyService)),
            _ => null
        };
    }
}

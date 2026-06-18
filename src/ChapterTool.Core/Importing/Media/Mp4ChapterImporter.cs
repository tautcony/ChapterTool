using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Media;

public sealed class Mp4ChapterImporter(IMp4ChapterReader reader) : IChapterImporter
{
    public string Id => "mp4";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".m4a",
        ".m4v"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(request.Path, cancellationToken);
        if (!read.Success)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(
                DiagnosticSeverity.Error,
                read.DiagnosticCode ?? "Mp4ReadFailed",
                read.Message ?? "MP4 chapter reader failed."));
        }

        if (read.Chapters.Count == 0)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "NoChaptersFound", "No MP4 chapters were read."));
        }

        var start = TimeSpan.Zero;
        var chapters = new List<Chapter>(read.Chapters.Count);
        foreach (var clip in read.Chapters)
        {
            chapters.Add(new Chapter(chapters.Count + 1, start, clip.Title));
            start += clip.Duration;
        }

        var info = new ChapterInfo(
            Path.GetFileNameWithoutExtension(request.Path),
            Path.GetFileName(request.Path),
            0,
            "MP4",
            0,
            start,
            chapters);
        var reference = new SourceMediaReference(Path.GetFileName(request.Path), Path.GetFileName(request.Path), request.Path);
        return new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, [new ChapterSourceOption("default", "MP4 Chapters", info, MediaReferences: [reference])])], []);
    }
}

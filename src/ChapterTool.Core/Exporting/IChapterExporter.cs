using ChapterTool.Core.Models;

namespace ChapterTool.Core.Exporting;

public interface IChapterExporter
{
    ChapterExportFormat Format { get; }

    ChapterExportResult Export(ChapterInfo chapterInfo, ChapterExportOptions options);
}

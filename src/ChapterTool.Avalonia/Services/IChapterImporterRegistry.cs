using ChapterTool.Core.Importing;

namespace ChapterTool.Avalonia.Services;

public interface IChapterImporterRegistry
{
    IChapterImporter? Resolve(string path);

    IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult);
}

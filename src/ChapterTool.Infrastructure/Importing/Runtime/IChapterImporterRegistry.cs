using ChapterTool.Core.Importing;

namespace ChapterTool.Infrastructure.Importing.Runtime;

public interface IChapterImporterRegistry
{
    IChapterImporter? Resolve(string path);

    IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult);
}

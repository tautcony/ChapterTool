using ChapterTool.Core.Importing;

namespace ChapterTool.Avalonia.Services;

public interface IChapterImporterRegistry
{
    IChapterImporter? Resolve(string path);
}

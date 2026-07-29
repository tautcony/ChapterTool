using ChapterTool.Core.Importing;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IChapterLoadService
{
    ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken);

    ValueTask<ChapterImportResult> LoadAsync(string path, IChapterImportProgressReporter? progress, CancellationToken cancellationToken) =>
        LoadAsync(path, cancellationToken);
}

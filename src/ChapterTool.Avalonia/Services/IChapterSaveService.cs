using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Services;

public interface IChapterSaveService
{
    ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken);
}

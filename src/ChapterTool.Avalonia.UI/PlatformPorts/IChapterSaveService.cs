using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface IChapterSaveService
{
    ValueTask<ChapterExportResult> SaveAsync(
        ChapterSet info,
        ChapterSerializationOptions options,
        string? directory,
        CancellationToken cancellationToken,
        string? sourcePath = null);
}

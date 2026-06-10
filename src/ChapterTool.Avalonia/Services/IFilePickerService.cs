namespace ChapterTool.Avalonia.Services;

public interface IFilePickerService
{
    ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken);

    ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken);
}

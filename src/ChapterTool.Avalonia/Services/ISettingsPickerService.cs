namespace ChapterTool.Avalonia.Services;

public interface ISettingsPickerService
{
    ValueTask<string?> PickDirectoryAsync(string title, CancellationToken cancellationToken);

    ValueTask<string?> PickExecutableAsync(string title, CancellationToken cancellationToken);
}

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ChapterTool.Avalonia.Localization;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaSettingsPickerService(Window owner, IAppLocalizer localizer) : ISettingsPickerService
{
    public async ValueTask<string?> PickDirectoryAsync(string title, CancellationToken cancellationToken)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        cancellationToken.ThrowIfCancellationRequested();
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickExecutableAsync(string title, CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(CreateExecutableOptions(title, localizer));

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    internal static FilePickerOpenOptions CreateExecutableOptions(string title, IAppLocalizer localizer) =>
        new()
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizer.GetString("FilePicker.ExecutableFiles"))
                {
                    Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]
                },
                FilePickerFileTypes.All
            ]
        };
}

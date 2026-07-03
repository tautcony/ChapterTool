using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaSettingsPickerService(Window owner) : ISettingsPickerService
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
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable files")
                {
                    Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]
                },
                FilePickerFileTypes.All
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}

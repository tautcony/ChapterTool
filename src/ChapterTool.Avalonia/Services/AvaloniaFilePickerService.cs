using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaFilePickerService(Window owner) : IFilePickerService
{
    public async ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Source",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Chapter and media files")
                {
                    Patterns = ["*.txt", "*.xml", "*.vtt", "*.cue", "*.flac", "*.tak", "*.mpls", "*.ifo", "*.xpl", "*.mkv", "*.mka", "*.mp4", "*.m4a", "*.m4v"]
                },
                FilePickerFileTypes.All
            ]
        });
        if (files.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return files[0].Path.LocalPath;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    public async ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Append MPLS",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MPLS playlist") { Patterns = ["*.mpls"] },
                FilePickerFileTypes.All
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Chapter Name Template",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text files") { Patterns = ["*.txt"] },
                FilePickerFileTypes.All
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Save Chapters To",
            AllowMultiple = false
        });

        cancellationToken.ThrowIfCancellationRequested();
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}

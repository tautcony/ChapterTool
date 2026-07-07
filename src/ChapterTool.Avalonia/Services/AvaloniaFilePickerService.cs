using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ChapterTool.Avalonia.Localization;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaFilePickerService(Window owner, IAppLocalizer localizer) : IFilePickerService
{
    public async ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(CreateSourceOptions(localizer));
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
        var files = await owner.StorageProvider.OpenFilePickerAsync(CreateMplsOptions(localizer));

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(CreateChapterNameTemplateOptions(localizer));

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickLuaExpressionScriptAsync(CancellationToken cancellationToken)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(CreateLuaExpressionScriptOptions(localizer));

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(CreateSaveDirectoryOptions(localizer));

        cancellationToken.ThrowIfCancellationRequested();
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    internal static FilePickerOpenOptions CreateSourceOptions(IAppLocalizer localizer) =>
        new()
        {
            Title = localizer.GetString("FilePicker.OpenSource.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizer.GetString("FilePicker.SourceFiles"))
                {
                    Patterns = ["*.txt", "*.xml", "*.vtt", "*.cue", "*.flac", "*.tak", "*.mpls", "*.ifo", "*.xpl", "*.mkv", "*.mka", "*.mp4", "*.m4a", "*.m4v"]
                },
                FilePickerFileTypes.All
            ]
        };

    internal static FilePickerOpenOptions CreateMplsOptions(IAppLocalizer localizer) =>
        new()
        {
            Title = localizer.GetString("FilePicker.AppendMpls.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizer.GetString("FilePicker.MplsPlaylist")) { Patterns = ["*.mpls"] },
                FilePickerFileTypes.All
            ]
        };

    internal static FilePickerOpenOptions CreateChapterNameTemplateOptions(IAppLocalizer localizer) =>
        new()
        {
            Title = localizer.GetString("FilePicker.OpenChapterNameTemplate.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizer.GetString("FilePicker.TextFiles")) { Patterns = ["*.txt"] },
                FilePickerFileTypes.All
            ]
        };

    internal static FilePickerOpenOptions CreateLuaExpressionScriptOptions(IAppLocalizer localizer) =>
        new()
        {
            Title = localizer.GetString("FilePicker.OpenLuaExpressionScript.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(localizer.GetString("FilePicker.LuaScriptFiles")) { Patterns = ["*.lua"] },
                FilePickerFileTypes.All
            ]
        };

    internal static FolderPickerOpenOptions CreateSaveDirectoryOptions(IAppLocalizer localizer) =>
        new()
        {
            Title = localizer.GetString("FilePicker.SaveChaptersTo.Title"),
            AllowMultiple = false
        };
}

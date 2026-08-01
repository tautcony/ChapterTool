using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public sealed class UnavailableClipboardService : IClipboardService
{
    public ValueTask<string?> GetTextAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask SetTextAsync(string value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class UnavailableSettingsPickerService : ISettingsPickerService
{
    public ValueTask<string?> PickDirectoryAsync(string title, CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask<string?> PickExecutableAsync(string title, CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);
}

public sealed class UnavailableFilePickerService : IFilePickerService
{
    public ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    public ValueTask<string?> PickLuaExpressionScriptAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);
}

public sealed class UnavailableShellService : IShellService
{
    public ValueTask OpenAsync(string target, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask RevealInFolderAsync(string filePath, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask OpenTerminalAsync(string directoryPath, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class UnavailableExternalToolLocator : IExternalToolLocator
{
    public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ExternalToolLocation(false, null));
}

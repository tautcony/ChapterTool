namespace ChapterTool.Core.Services;

public interface IShellService
{
    ValueTask OpenAsync(string target, CancellationToken cancellationToken);

    ValueTask RevealInFolderAsync(string filePath, CancellationToken cancellationToken);

    ValueTask OpenTerminalAsync(string directoryPath, CancellationToken cancellationToken);
}

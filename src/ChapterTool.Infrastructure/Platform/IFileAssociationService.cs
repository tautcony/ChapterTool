namespace ChapterTool.Infrastructure.Platform;

public interface IFileAssociationService
{
    ValueTask<FileAssociationResult> RegisterAsync(
        string extension,
        string progId,
        string description,
        CancellationToken cancellationToken);

    ValueTask<FileAssociationResult> UnregisterAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken);

    ValueTask<FileAssociationResult> IsRegisteredAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken);
}

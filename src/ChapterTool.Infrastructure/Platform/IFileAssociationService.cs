namespace ChapterTool.Infrastructure.Platform;

public interface IFileAssociationService
{
    ValueTask<FileAssociationResult> RegisterAsync(
        string extension,
        string progId,
        string description,
        CancellationToken cancellationToken);
}

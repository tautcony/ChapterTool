using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Infrastructure.Platform;

public sealed class UnsupportedFileAssociationService : IFileAssociationService
{
    public ValueTask<FileAssociationResult> RegisterAsync(
        string extension,
        string progId,
        string description,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new FileAssociationResult(
            false,
            [Unsupported("File association is not supported on this platform.")]));
    }

    private static ChapterDiagnostic Unsupported(string message) =>
        new(DiagnosticSeverity.Warning, "UnsupportedPlatform", message);
}

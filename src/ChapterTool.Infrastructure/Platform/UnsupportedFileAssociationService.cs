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
            [Unsupported()]));
    }

    public ValueTask<FileAssociationResult> UnregisterAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new FileAssociationResult(
            false,
            [Unsupported()]));
    }

    public ValueTask<FileAssociationResult> IsRegisteredAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new FileAssociationResult(
            false,
            [Unsupported()]));
    }

    private static ChapterDiagnostic Unsupported() =>
        new(DiagnosticSeverity.Warning, "UnsupportedPlatform",
            "File association is not supported on this platform.");
}

using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Infrastructure.Platform;

public sealed class UnsupportedPrivilegeService : IPrivilegeService
{
    public bool IsAdministrator => false;

    public ValueTask<PrivilegeResult> RequestElevationAsync(string operationId, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PrivilegeResult(
            false,
            [new ChapterDiagnostic(DiagnosticSeverity.Warning, "UnsupportedPlatform", "Privilege elevation is not supported on this platform.")]));
    }
}

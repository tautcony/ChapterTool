using System.Runtime.Versioning;
using ChapterTool.Core.Diagnostics;
using Microsoft.Win32;

namespace ChapterTool.Infrastructure.Platform;

/// <summary>
/// Windows implementation of <see cref="IFileAssociationService"/> that writes
/// per-user file associations under <c>HKCU\Software\Classes</c>. No administrator
/// privileges are required for per-user registration.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsFileAssociationService : IFileAssociationService
{
    private const string ClassesRoot = @"Software\Classes";

    public ValueTask<FileAssociationResult> RegisterAsync(
        string extension,
        string progId,
        string description,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Register ProgId: HKCU\Software\Classes\<progId>
            using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{progId}"))
            {
                progIdKey.SetValue(string.Empty, description);
            }

            // Map extension to ProgId: HKCU\Software\Classes\<extension>
            using (var extKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{extension}"))
            {
                extKey.SetValue(string.Empty, progId);
            }

            return ValueTask.FromResult(new FileAssociationResult(true, []));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new FileAssociationResult(
                false,
                [new ChapterDiagnostic(
                    DiagnosticSeverity.Error,
                    "FileAssociationRegistrationFailed",
                    $"Failed to register file association for {extension}: {ex.Message}")]));
        }
    }

    public ValueTask<FileAssociationResult> UnregisterAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Remove extension mapping
            Registry.CurrentUser.DeleteSubKey($@"{ClassesRoot}\{extension}", throwOnMissingSubKey: false);

            // Remove ProgId
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{progId}", throwOnMissingSubKey: false);

            return ValueTask.FromResult(new FileAssociationResult(true, []));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new FileAssociationResult(
                false,
                [new ChapterDiagnostic(
                    DiagnosticSeverity.Error,
                    "FileAssociationUnregistrationFailed",
                    $"Failed to unregister file association for {extension}: {ex.Message}")]));
        }
    }

    public ValueTask<FileAssociationResult> IsRegisteredAsync(
        string extension,
        string progId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var extKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{extension}");
            if (extKey?.GetValue(string.Empty) is string registeredProgId
                && string.Equals(registeredProgId, progId, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(new FileAssociationResult(true, []));
            }

            return ValueTask.FromResult(new FileAssociationResult(false, []));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new FileAssociationResult(
                false,
                [new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    "FileAssociationCheckFailed",
                    $"Failed to check file association for {extension}: {ex.Message}")]));
        }
    }
}

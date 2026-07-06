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
    private const string OwnerValueName = "ChapterToolOwner";

    public ValueTask<FileAssociationResult> RegisterAsync(
        string extension,
        string progId,
        string description,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var normalizedExtension = NormalizeExtension(extension);
            var applicationPath = CurrentApplicationPath();

            using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{progId}"))
            {
                progIdKey.SetValue(string.Empty, description);
                progIdKey.SetValue(OwnerValueName, progId);
            }

            using (var iconKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{progId}\DefaultIcon"))
            {
                iconKey.SetValue(string.Empty, $"{applicationPath},0");
            }

            using (var commandKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{progId}\shell\open\command"))
            {
                commandKey.SetValue(string.Empty, BuildOpenCommand(applicationPath));
            }

            using (var extKey = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{normalizedExtension}"))
            {
                extKey.SetValue(string.Empty, progId);
                extKey.SetValue(OwnerValueName, progId);
            }

            NotifyShellAssociationChanged();
            return ValueTask.FromResult(new FileAssociationResult(true, []));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new FileAssociationResult(
                false,
                [new ChapterDiagnostic(
                    DiagnosticSeverity.Error,
                    "FileAssociationRegistrationFailed",
                    $"Failed to register file association for {NormalizeExtension(extension)}: {ex.Message}")]));
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
            var normalizedExtension = NormalizeExtension(extension);
            using (var extKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{normalizedExtension}", writable: true))
            {
                if (ExtensionBelongsToProgId(extKey, progId))
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{normalizedExtension}", throwOnMissingSubKey: false);
                }
                else if (extKey?.GetValue(OwnerValueName) is not null)
                {
                    extKey.DeleteValue(OwnerValueName, throwOnMissingValue: false);
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{progId}", throwOnMissingSubKey: false);

            NotifyShellAssociationChanged();
            return ValueTask.FromResult(new FileAssociationResult(true, []));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new FileAssociationResult(
                false,
                [new ChapterDiagnostic(
                    DiagnosticSeverity.Error,
                    "FileAssociationUnregistrationFailed",
                    $"Failed to unregister file association for {NormalizeExtension(extension)}: {ex.Message}")]));
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
            var normalizedExtension = NormalizeExtension(extension);
            using var extKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{normalizedExtension}");
            using var commandKey = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{progId}\shell\open\command");
            if (extKey?.GetValue(string.Empty) is string registeredProgId
                && string.Equals(registeredProgId, progId, StringComparison.OrdinalIgnoreCase)
                && commandKey?.GetValue(string.Empty) is string command
                && !string.IsNullOrWhiteSpace(command))
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
                    $"Failed to check file association for {NormalizeExtension(extension)}: {ex.Message}")]));
        }
    }

    internal static string BuildOpenCommand(string applicationPath) => $"\"{applicationPath}\" \"%1\"";

    internal static bool ExtensionBelongsToProgId(RegistryKey? extensionKey, string progId) =>
        extensionKey?.GetValue(string.Empty) is string registeredProgId
            && string.Equals(registeredProgId, progId, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}";

    private static string CurrentApplicationPath() =>
        Environment.ProcessPath
        ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
        ?? AppContext.BaseDirectory;

    private static void NotifyShellAssociationChanged() =>
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}

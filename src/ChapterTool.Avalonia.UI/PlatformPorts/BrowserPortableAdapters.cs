using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

/// <summary>Describes a browser file before its bytes are requested.</summary>
public sealed record BrowserFileHandle(string Name, long Length, object NativeHandle);

/// <summary>Reads browser files after the size policy accepts them.</summary>
public interface IBrowserFileAccess
{
    ValueTask<BrowserFileHandle?> PickAsync(CancellationToken cancellationToken);

    ValueTask<BrowserFileHandle?> FromDropAsync(object drop, CancellationToken cancellationToken);

    ValueTask<byte[]> ReadAsync(BrowserFileHandle file, CancellationToken cancellationToken);
}

/// <summary>Converts browser file handles to bounded typed source documents.</summary>
public sealed class BrowserChapterSourcePicker(
    IBrowserFileAccess fileAccess,
    long maxBytes = PortableInputPolicy.MaxBytes) : IChapterSourcePicker
{
    public ValueTask<ChapterSourceDocument?> PickSourceAsync(CancellationToken cancellationToken) =>
        CreateSourceAsync(() => fileAccess.PickAsync(cancellationToken), cancellationToken);

    public ValueTask<ChapterSourceDocument?> FromDropAsync(object drop, CancellationToken cancellationToken) =>
        CreateSourceAsync(() => fileAccess.FromDropAsync(drop, cancellationToken), cancellationToken);

    public async ValueTask<ChapterSourceReadResult> ReadSourceAsync(
        Func<ValueTask<BrowserFileHandle?>> acquire,
        CancellationToken cancellationToken)
    {
        BrowserFileHandle? file;
        try
        {
            file = await acquire();
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return Failed(ChapterDiagnosticCode.InputReadFailed, "The browser blocked the file read.", exception.Message);
        }

        if (file is null)
        {
            return new ChapterSourceReadResult(null, []);
        }

        if (file.Length <= 0)
        {
            return Failed(ChapterDiagnosticCode.InputEmpty, "The selected file is empty.", file.Name);
        }

        if (!PortableInputPolicy.IsWithinLimit(file.Length) || file.Length > maxBytes)
        {
            return Failed(ChapterDiagnosticCode.InputTooLarge, "The selected file exceeds the browser input limit.", file.Name);
        }

        try
        {
            var bytes = await fileAccess.ReadAsync(file, cancellationToken);
            if (bytes.Length == 0)
            {
                return Failed(ChapterDiagnosticCode.InputEmpty, "The selected file is empty.", file.Name);
            }

            if (bytes.LongLength > maxBytes)
            {
                return Failed(ChapterDiagnosticCode.InputTooLarge, "The selected file exceeds the browser input limit.", file.Name);
            }

            return ChapterSourceReadResult.FromSource(new BufferedChapterSource(file.Name, bytes));
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return Failed(ChapterDiagnosticCode.InputReadFailed, "The browser blocked the file read.", exception.Message);
        }
    }

    private async ValueTask<ChapterSourceDocument?> CreateSourceAsync(
        Func<ValueTask<BrowserFileHandle?>> acquire,
        CancellationToken cancellationToken)
    {
        var result = await ReadSourceAsync(acquire, cancellationToken);
        return result.Success ? result.Source : null;
    }

    private static ChapterSourceReadResult Failed(ChapterDiagnosticCode code, string message, string? details) =>
        ChapterSourceReadResult.Failed(new ChapterDiagnostic(
            DiagnosticSeverity.Error,
            code,
            message,
            Details: details));
}

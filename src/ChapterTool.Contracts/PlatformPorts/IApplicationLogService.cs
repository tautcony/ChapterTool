using Microsoft.Extensions.Logging;

namespace ChapterTool.Contracts.PlatformPorts;

public interface IApplicationLogService
{
    event EventHandler<ApplicationLogEntry>? EntryAdded;

    /// <summary>Raised after the entry history has been cleared.</summary>
    event EventHandler? Cleared;

    IReadOnlyList<ApplicationLogEntry> Entries { get; }

    void Clear();
}

public enum LogExportFormat
{
    Json,
    Csv
}

public sealed record ApplicationLogExportRequest(
    LogExportFormat Format,
    IReadOnlyList<ApplicationLogEntry> Entries);

public sealed record ApplicationLogExportResult(bool Succeeded, string? Path, string? Error)
{
    public static ApplicationLogExportResult Success(string path) => new(true, path, null);

    public static ApplicationLogExportResult Failure(string error) => new(false, null, error);
}

public interface IApplicationLogExporter
{
    ValueTask<ApplicationLogExportResult> ExportAsync(
        ApplicationLogExportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?>? Arguments = null,
    string? TechnicalDetail = null,
    string? Category = null,
    int EventId = 0,
    string? EventName = null,
    string? ExceptionText = null,
    IReadOnlyDictionary<string, object?>? StructuredState = null,
    string? Operation = null);

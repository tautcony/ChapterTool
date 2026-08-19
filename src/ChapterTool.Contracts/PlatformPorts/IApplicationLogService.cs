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

public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object?>? Arguments = null,
    string? TechnicalDetail = null,
    string? Category = null,
    int EventId = 0,
    string? EventName = null,
    string? ExceptionText = null,
    IReadOnlyDictionary<string, object?>? StructuredState = null,
    string? Operation = null);

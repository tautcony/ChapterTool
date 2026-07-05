using Microsoft.Extensions.Logging;

namespace ChapterTool.Core.Services;

public interface IApplicationLogService
{
    event EventHandler<ApplicationLogEntry>? EntryAdded
    {
        add { }
        remove { }
    }

    IReadOnlyList<ApplicationLogEntry> Entries { get; }

    string Format(Func<ApplicationLogEntry, string>? formatter = null);

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
    IReadOnlyDictionary<string, object?>? StructuredState = null);

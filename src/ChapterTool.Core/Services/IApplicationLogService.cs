namespace ChapterTool.Core.Services;

public interface IApplicationLogService
{
    IReadOnlyList<ApplicationLogEntry> Entries { get; }

    void Add(string message);

    void Add(string key, IReadOnlyDictionary<string, object?> arguments, string? technicalDetail = null);

    string Format(Func<ApplicationLogEntry, string>? formatter = null);

    void Clear();
}

public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    string Message,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object?>? Arguments = null,
    string? TechnicalDetail = null);

using ChapterTool.Core.Services;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Infrastructure.Platform;

public sealed class ApplicationLogPanelProvider(
    int capacity = ApplicationLogPanelProvider.DefaultCapacity,
    LogLevel minimumLevel = LogLevel.Information)
    : IApplicationLogService, ILoggerProvider
{
    private const int DefaultCapacity = 500;
    private readonly Lock gate = new();
    private readonly int capacity = Math.Max(1, capacity);
    private readonly LogLevel minimumLevel = minimumLevel;
    private readonly List<ApplicationLogEntry> entries = [];

    public event EventHandler<ApplicationLogEntry>? EntryAdded;

    public IReadOnlyList<ApplicationLogEntry> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new ApplicationLogPanelLogger(this, categoryName);

    public string Format(Func<ApplicationLogEntry, string>? formatter = null)
    {
        IReadOnlyList<ApplicationLogEntry> snapshot;
        lock (gate)
        {
            snapshot = entries.ToList();
        }

        return string.Join(
            Environment.NewLine,
            snapshot.Select(entry =>
            {
                var message = formatter is null ? entry.Message : formatter(entry);
                return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {message}";
            }));
    }

    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
        }
    }

    public void Dispose()
    {
    }

    private void Capture<TState>(
        string category,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel < minimumLevel || logLevel == LogLevel.None)
        {
            return;
        }

        var structuredState = StructuredState(state);
        var messageKey = StateString(structuredState, "MessageKey");
        var technicalDetail = StateString(structuredState, "TechnicalDetail");
        var arguments = Arguments(structuredState);
        var message = string.IsNullOrWhiteSpace(messageKey) ? formatter(state, exception).Trim() : messageKey.Trim();
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var entry = new ApplicationLogEntry(
            DateTimeOffset.Now,
            logLevel,
            string.IsNullOrWhiteSpace(message) ? exception!.Message : message,
            messageKey,
            arguments,
            technicalDetail,
            category,
            eventId.Id,
            eventId.Name,
            exception?.ToString(),
            structuredState);

        lock (gate)
        {
            entries.Add(entry);
            if (entries.Count > capacity)
            {
                entries.RemoveRange(0, entries.Count - capacity);
            }
        }

        var handler = EntryAdded;
        handler?.Invoke(this, entry);
    }

    private static Dictionary<string, object?> StructuredState<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return pairs
                .Where(static pair => !string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        }

        return state is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["State"] = state };
    }

    private static Dictionary<string, object?> Arguments(IReadOnlyDictionary<string, object?> structuredState) =>
        structuredState
            .Where(static pair =>
                !string.Equals(pair.Key, "MessageKey", StringComparison.Ordinal) &&
                !string.Equals(pair.Key, "TechnicalDetail", StringComparison.Ordinal))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    private static string? StateString(IReadOnlyDictionary<string, object?> state, string key) =>
        state.TryGetValue(key, out var value) ? value?.ToString() : null;

    private sealed class ApplicationLogPanelLogger(ApplicationLogPanelProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= owner.minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner.Capture(category, logLevel, eventId, state, exception, formatter);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

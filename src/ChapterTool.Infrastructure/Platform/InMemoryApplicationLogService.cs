using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class InMemoryApplicationLogService : IApplicationLogService
{
    private readonly List<ApplicationLogEntry> entries = [];

    public IReadOnlyList<ApplicationLogEntry> Entries => entries;

    public void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        entries.Add(new ApplicationLogEntry(DateTimeOffset.Now, message.Trim()));
    }

    public void Add(string key, IReadOnlyDictionary<string, object?> arguments, string? technicalDetail = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        entries.Add(new ApplicationLogEntry(
            DateTimeOffset.Now,
            key.Trim(),
            key.Trim(),
            new Dictionary<string, object?>(arguments, StringComparer.Ordinal),
            technicalDetail));
    }

    public string Format(Func<ApplicationLogEntry, string>? formatter = null) =>
        string.Join(
            Environment.NewLine,
            entries.Select(entry =>
            {
                var message = formatter is null ? entry.Message : formatter(entry);
                return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {message}";
            }));

    public void Clear() => entries.Clear();
}

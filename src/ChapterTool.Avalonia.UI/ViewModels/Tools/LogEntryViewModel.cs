using System.Collections;
using System.Globalization;
using System.Text;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed record LogPropertyViewModel(string Name, string Value);

public sealed record LogStructuredNodeViewModel(
    string Name,
    string Value,
    IReadOnlyList<LogStructuredNodeViewModel> Children,
    bool IsInitiallyExpanded)
{
    public bool HasChildren => Children.Count > 0;
}

public sealed class LogEntryViewModel(
    ApplicationLogEntry entry,
    IAppLocalizer localizer,
    IAppLocalizer contentLocalizer)
    : ObservableViewModel
{
    private string? rawText;
    private string? searchableText;
    private string? formattedImportEntries;

    public ApplicationLogEntry Entry => entry;

    public string Timestamp => entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string Time => entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string LevelText => localizer.GetString(LevelKey(entry.Level));

    public string Summary
    {
        get
        {
            if (entry.Arguments is { } arguments && arguments.TryGetValue("message", out var message)
                && !string.IsNullOrWhiteSpace(message?.ToString()))
            {
                return message.ToString()!;
            }

            return entry.MessageKey is null
                ? entry.Message
                : contentLocalizer.Format(entry.MessageKey, entry.Arguments);
        }
    }

    public string Category => entry.Category ?? string.Empty;

    public string EventName => entry.EventName ?? string.Empty;

    public string EventDisplay => string.IsNullOrWhiteSpace(EventName)
        ? entry.EventId == 0 ? string.Empty : entry.EventId.ToString(CultureInfo.InvariantCulture)
        : entry.EventId == 0 ? EventName : $"{EventName} ({entry.EventId})";

    public string Operation => entry.Operation ?? string.Empty;

    public bool HasOperation => !string.IsNullOrWhiteSpace(Operation);

    public string Context => string.IsNullOrWhiteSpace(EventName)
        ? Category
        : string.IsNullOrWhiteSpace(Category)
            ? EventDisplay
            : $"{Category} / {EventDisplay}";

    public string Details
    {
        get
        {
            var builder = new StringBuilder();
            Append(builder, entry.TechnicalDetail);
            Append(builder, entry.ExceptionText);
            if (TryGetFormattedImportEntries(out var formattedImport))
            {
                Append(builder, formattedImport);
            }
            else if (HasStructuredTree)
            {
                Append(builder, FormatNodes(StructuredTree));
            }

            return builder.ToString();
        }
    }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public string TechnicalDetail => entry.TechnicalDetail?.Trim() ?? string.Empty;

    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(TechnicalDetail);

    public bool HasOverviewContent => HasTechnicalDetail || HasStructuredProperties;

    public string ExceptionText => entry.ExceptionText?.Trim() ?? string.Empty;

    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionText);

    /// <summary>Complete, deterministic JSON for diagnostics and clipboard export.</summary>
    public string RawText => rawText ??= LogRawValueFormatter.Format(entry, Summary);

    public IReadOnlyList<LogPropertyViewModel> StructuredProperties =>
        entry.StructuredState is not { Count: > 0 }
            ? []
            : CreateStructuredProperties();

    public bool HasStructuredProperties => StructuredProperties.Count > 0;

    public IReadOnlyList<LogStructuredNodeViewModel> StructuredTree =>
        entry.StructuredState is not { Count: > 0 }
            ? []
            : CreateNodes(
                entry.StructuredState
                    .Where(static pair => !IsHiddenStructuredKey(pair.Key))
                    .Where(static pair => IsContainer(pair.Value))
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal));

    public bool HasStructuredTree => StructuredTree.Count > 0;

    public bool IsInformation => entry.Level == LogLevel.Information;

    public bool IsWarning => entry.Level == LogLevel.Warning;

    public bool IsError => entry.Level >= LogLevel.Error;

    public bool Matches(LogSeverityFilter filter) => filter switch
    {
        LogSeverityFilter.All => true,
        LogSeverityFilter.Information => entry.Level == LogLevel.Information,
        LogSeverityFilter.Warning => entry.Level == LogLevel.Warning,
        LogSeverityFilter.Error => entry.Level >= LogLevel.Error,
        _ => false
    };

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(SearchableText, query);
    }

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(LevelText));
        rawText = null;
        searchableText = null;
        formattedImportEntries = null;
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(RawText));
        OnPropertyChanged(nameof(TechnicalDetail));
        OnPropertyChanged(nameof(ExceptionText));
        OnPropertyChanged(nameof(StructuredProperties));
        OnPropertyChanged(nameof(StructuredTree));
        OnPropertyChanged(nameof(HasStructuredTree));
        OnPropertyChanged(nameof(Context));
    }

    private string SearchableText => searchableText ??= string.Join(
        Environment.NewLine,
        Summary,
        Category,
        Operation,
        EventDisplay,
        entry.MessageKey,
        entry.TechnicalDetail,
        entry.ExceptionText,
        RawText);

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<LogPropertyViewModel> CreateStructuredProperties()
    {
        var properties = entry.StructuredState!
            .Where(static pair => !IsOverviewHiddenKey(pair.Key) && !IsContainer(pair.Value))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new LogPropertyViewModel(HumanizeKey(pair.Key), FormatValue(pair.Value)))
            .ToList();

        if (TryGetFormattedImportEntries(out var formattedImport))
        {
            properties.Insert(0, new LogPropertyViewModel("Import Entries", formattedImport));
        }

        return properties;
    }

    private bool TryGetFormattedImportEntries(out string value)
    {
        value = formattedImportEntries ??= ImportSummaryFormatter.Format(entry);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(value.Trim());
    }

    private static string LevelKey(LogLevel level) => level switch
    {
        LogLevel.Warning => "Tool.Log.FilterWarning",
        LogLevel.Error or LogLevel.Critical => "Tool.Log.FilterError",
        _ => "Tool.Log.FilterInformation"
    };

    private static IReadOnlyList<LogStructuredNodeViewModel> CreateNodes(
        IEnumerable<KeyValuePair<string, object?>> values,
        int depth = 0)
    {
        return
        [
            .. values
                .Select(pair => CreateNode(pair.Key, pair.Value, depth))
        ];
    }

    private static string FormatNodes(IEnumerable<LogStructuredNodeViewModel> nodes, int depth = 0)
    {
        var lines = new List<string>();
        foreach (var node in nodes)
        {
            var value = string.IsNullOrEmpty(node.Value) ? string.Empty : $" = {node.Value}";
            lines.Add($"{new string(' ', depth * 2)}{node.Name}{value}");
            if (node.HasChildren)
            {
                lines.Add(FormatNodes(node.Children, depth + 1));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static LogStructuredNodeViewModel CreateNode(string name, object? value, int depth)
    {
        if (depth >= 32)
        {
            return new LogStructuredNodeViewModel(HumanizeKey(name), "[depth limit]", [], false);
        }

        var children = CreateChildren(value, depth + 1);
        return new LogStructuredNodeViewModel(
            HumanizeKey(name),
            FormatContainer(value, children),
            children,
            depth == 0 && children.Count > 0);
    }

    private static IReadOnlyList<LogStructuredNodeViewModel> CreateChildren(object? value, int depth)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                return CreateNodes(
                    readOnlyDictionary
                        .Where(static pair => !IsHiddenStructuredKey(pair.Key))
                        .OrderBy(static pair => pair.Key, StringComparer.Ordinal),
                    depth);
            case IDictionary dictionary:
            {
                var pairs = new List<KeyValuePair<string, object?>>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    pairs.Add(new KeyValuePair<string, object?>(entry.Key?.ToString() ?? string.Empty, entry.Value));
                }

                return CreateNodes(pairs.OrderBy(static pair => pair.Key, StringComparer.Ordinal), depth);
            }
        }

        if (value is IEnumerable enumerable and not string)
        {
            var index = 0;

            return [.. from object? item in enumerable select CreateNode($"#{++index}", item, depth)];
        }

        return [];
    }

    private static string FormatContainer(object? value, IReadOnlyList<LogStructuredNodeViewModel> children)
    {
        if (children.Count == 0)
        {
            return FormatValue(value);
        }

        return value is IDictionary or IReadOnlyDictionary<string, object?>
            ? CountLabel(children.Count, "field")
            : CountLabel(children.Count, "item");
    }

    private static string CountLabel(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";

    private static string HumanizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key[0] == '#')
        {
            return key;
        }

        if (string.Equals(key, "fps", StringComparison.OrdinalIgnoreCase))
        {
            return "FPS";
        }

        if (string.Equals(key, "id", StringComparison.OrdinalIgnoreCase))
        {
            return "ID";
        }

        var builder = new StringBuilder(key.Length + 8);
        for (var index = 0; index < key.Length; index++)
        {
            var character = key[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(key[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(index == 0 ? char.ToUpperInvariant(character) : character);
        }

        return builder.ToString();
    }

    private static bool IsHiddenStructuredKey(string key) =>
        string.Equals(key, "MessageKey", StringComparison.Ordinal) ||
        string.Equals(key, "TechnicalDetail", StringComparison.Ordinal) ||
        string.Equals(key, "Operation", StringComparison.Ordinal) ||
        string.Equals(key, "operation", StringComparison.Ordinal);

    /// <summary>
    /// Keys hidden from the flat overview list: infrastructure keys, the operation tag
    /// (already shown as a badge) and the folded import details (available in the tree).
    /// </summary>
    private static bool IsOverviewHiddenKey(string key) =>
        IsHiddenStructuredKey(key) ||
        string.Equals(key, "severity", StringComparison.Ordinal) ||
        string.Equals(key, "message", StringComparison.Ordinal) ||
        string.Equals(key, "details", StringComparison.Ordinal);

    private static bool IsContainer(object? value) =>
        value is IDictionary or IReadOnlyDictionary<string, object?> or IEnumerable and not string;

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        _ => value.ToString() ?? string.Empty
    };
}

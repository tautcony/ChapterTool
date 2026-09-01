using System.Collections;
using System.Globalization;
using System.Text;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed record LogPropertyViewModel(string Name, string Value);

public sealed record LogHighlightRun(string Text, bool IsMatch);

public sealed class LogEntryViewModel(
    ApplicationLogEntry entry,
    IAppLocalizer localizer,
    IAppLocalizer contentLocalizer)
    : ObservableViewModel
{
    private const int MaxSearchDepth = 32;
    private const int MaxSearchValues = 4096;
    private const int MaxCompactSummaryLength = 180;

    private static readonly Dictionary<string, string> SpecialMessageMap = new(StringComparer.Ordinal)
    {
        ["Log.SettingsLoaded"] = "Settings loaded",
        ["Log.LoadingSource"] = "Loading source",
        ["Log.SavingChapters"] = "Saving chapters",
        ["Log.AppendingMpls"] = "Appending MPLS",
        ["Log.Diagnostic"] = "Diagnostic",
        ["Log.TemplateLoaded"] = "Template loaded",
        ["Log.TemplateLoadFailed"] = "Template load failed",
        ["Log.LanguageSet"] = "Language changed",
        ["Log.UnexpectedError"] = "Unexpected error"
    };

    private string? rawText;
    private string? searchableText;
    private string? additionalSearchableText;
    private string? compactSummary;

    public ApplicationLogEntry Entry => entry;

    public string Time => entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

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

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    /// <summary>
    /// Gets the short message used by the list. Technical arguments stay in the
    /// inspector so a long path or import payload cannot take over the feed.
    /// </summary>
    public string CompactSummary => compactSummary ??= CreateCompactSummary();

    public string Category => entry.Category ?? string.Empty;

    public string EventName => entry.EventName ?? string.Empty;

    public string EventDisplay => string.IsNullOrWhiteSpace(EventName)
        ? entry.EventId == 0 ? string.Empty : entry.EventId.ToString(CultureInfo.InvariantCulture)
        : entry.EventId == 0 ? EventName : $"{EventName} ({entry.EventId})";

    public string Operation => entry.Operation ?? string.Empty;

    public bool HasOperation => !string.IsNullOrWhiteSpace(Operation);

    public bool HasContext => !string.IsNullOrWhiteSpace(Context);

    /// <summary>Gets only identity that helps scan a row. The full category stays in the inspector.</summary>
    public string RowContext
    {
        get
        {
            var parts = new List<string>(capacity: 2);
            AddContextPart(parts, Operation);
            AddContextPart(parts, EventDisplay);
            return string.Join(" / ", parts);
        }
    }

    public bool HasRowContext => !string.IsNullOrWhiteSpace(RowContext);

    public string Context
    {
        get
        {
            var parts = new List<string>(capacity: 3);
            AddContextPart(parts, Operation);
            AddContextPart(parts, Category);
            AddContextPart(parts, EventDisplay);
            return string.Join(" / ", parts);
        }
    }

    public string IdentityText => Context;

    public string AccessibleName => localizer.Format(
            "Tool.Log.EntryAccessibleName",
            new Dictionary<string, object?>
            {
                ["level"] = LevelText,
                ["summary"] = CompactSummary,
                ["time"] = Time,
                ["context"] = Context
            })
        + (HasAdditionalSearchMatch ? $" {SearchMatchIndicatorText}" : string.Empty);

    public string DetailsActionText => localizer.GetString("Tool.Log.OpenDetails");

    public string SearchMatchIndicatorText => localizer.GetString("Tool.Log.SearchMatchAdditional");

    public bool HasAdditionalSearchMatch
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(AccessibleName));
            }
        }
    }

    public IReadOnlyList<LogHighlightRun> SummaryRuns
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    public bool HasInspectorContent => HasTechnicalDetail || HasException || HasStructuredProperties;

    public string TechnicalDetail => entry.TechnicalDetail?.Trim() ?? string.Empty;

    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(TechnicalDetail);

    public string ExceptionText => entry.ExceptionText?.Trim() ?? string.Empty;

    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionText);

    /// <summary>Complete, deterministic JSON for diagnostics and clipboard export.</summary>
    public string RawText => rawText ??= LogRawValueFormatter.Format(entry, Summary);

    public IReadOnlyList<LogPropertyViewModel> StructuredProperties =>
        field ??= entry.StructuredState is not { Count: > 0 }
            ? []
            : CreateStructuredProperties();

    public bool HasStructuredProperties => StructuredProperties.Count > 0;

    public bool HasNoStructuredProperties => !HasStructuredProperties;

    public bool ShowNoDetails => !HasInspectorContent;

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

    public void ApplySearchHighlight(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            HasAdditionalSearchMatch = false;
            SummaryRuns = [new LogHighlightRun(CompactSummary, false)];
            return;
        }

        HasAdditionalSearchMatch = !Contains(CompactSummary, query)
            && Contains(AdditionalSearchableText, query);

        var runs = new List<LogHighlightRun>();
        var offset = 0;
        while (offset < CompactSummary.Length)
        {
            var match = CompactSummary.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                runs.Add(new LogHighlightRun(CompactSummary[offset..], false));
                break;
            }

            if (match > offset)
            {
                runs.Add(new LogHighlightRun(CompactSummary[offset..match], false));
            }

            runs.Add(new LogHighlightRun(CompactSummary.Substring(match, query.Length), true));
            offset = match + query.Length;
        }

        SummaryRuns = runs;
    }

    public void RefreshLocalizedProperties()
    {
        rawText = null;
        searchableText = null;
        additionalSearchableText = null;
        compactSummary = null;

        // Clear derived text before raising notifications so bindings never read a stale
        // accessible name or summary during a culture switch.
        OnPropertyChanged(nameof(LevelText));
        OnPropertyChanged(nameof(DetailsActionText));
        OnPropertyChanged(nameof(SearchMatchIndicatorText));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSummary));
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(RawText));
        OnPropertyChanged(nameof(TechnicalDetail));
        OnPropertyChanged(nameof(ExceptionText));
        OnPropertyChanged(nameof(StructuredProperties));
        OnPropertyChanged(nameof(Context));
        OnPropertyChanged(nameof(RowContext));
        OnPropertyChanged(nameof(IdentityText));
        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(HasRowContext));
        ApplySearchHighlight(string.Empty);
    }

    private string SearchableText => searchableText ??= BuildSearchableText();

    private string AdditionalSearchableText => additionalSearchableText ??= BuildAdditionalSearchableText();

    private string BuildSearchableText()
    {
        var values = new List<string>
        {
            Summary,
            CompactSummary,
            AdditionalSearchableText
        };

        return string.Join(Environment.NewLine, values);
    }

    private string BuildAdditionalSearchableText()
    {
        var values = new List<string>
        {
            entry.Message,
            Summary,
            Category,
            Operation,
            EventDisplay,
            entry.MessageKey ?? string.Empty,
            entry.TechnicalDetail ?? string.Empty,
            entry.ExceptionText ?? string.Empty
        };

        var budget = MaxSearchValues;
        if (entry.StructuredState is not null)
        {
            AppendSearchablePairs(values, entry.StructuredState, ref budget);
        }

        // Some callers construct ApplicationLogEntry directly and provide arguments
        // without mirroring them into StructuredState. Keep those fields searchable.
        if (entry.Arguments is not null)
        {
            AppendSearchablePairs(values, entry.Arguments, ref budget);
        }

        return string.Join(Environment.NewLine, values);
    }

    private static void AppendSearchablePairs(
        List<string> values,
        IEnumerable<KeyValuePair<string, object?>> pairs,
        ref int budget)
    {
        foreach (var pair in pairs)
        {
            if (budget <= 0)
            {
                break;
            }

            values.Add(pair.Key);
            AppendSearchableValue(
                values,
                pair.Value,
                depth: 0,
                new HashSet<object>(ReferenceEqualityComparer.Instance),
                ref budget);
        }
    }

    private static void AppendSearchableValue(
        List<string> values,
        object? value,
        int depth,
        HashSet<object> path,
        ref int budget)
    {
        if (value is null || budget-- <= 0)
        {
            return;
        }

        if (value is string text)
        {
            values.Add(text);
            return;
        }

        if (depth >= MaxSearchDepth)
        {
            values.Add("[depth limit]");
            return;
        }

        var runtimeType = value.GetType();
        var trackReference = !runtimeType.IsValueType;
        if (trackReference && !path.Add(value))
        {
            values.Add("[cycle]");
            return;
        }

        try
        {
            if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                foreach (var pair in readOnlyDictionary)
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    values.Add(pair.Key);
                    AppendSearchableValue(values, pair.Value, depth + 1, path, ref budget);
                }

                return;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry pair in dictionary)
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    values.Add(pair.Key?.ToString() ?? string.Empty);
                    AppendSearchableValue(values, pair.Value, depth + 1, path, ref budget);
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (budget <= 0)
                    {
                        break;
                    }

                    AppendSearchableValue(values, item, depth + 1, path, ref budget);
                }

                return;
            }

            values.Add(value.ToString() ?? string.Empty);
        }
        finally
        {
            if (trackReference)
            {
                path.Remove(value);
            }
        }
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private string? GetSpecialMessageCompact(string? messageKey)
    {
        return messageKey switch
        {
            null => null,
            "Log.ImportSummary" => CompactImportSummary(),
            _ => SpecialMessageMap.GetValueOrDefault(messageKey)
        };
    }

    private string CreateCompactSummary()
    {
        if (TryArgument("Summary", out var explicitSummary) || TryArgument("summary", out explicitSummary))
        {
            return TruncateSummary(explicitSummary);
        }

        if (TryArgument("message", out var message) && !string.IsNullOrWhiteSpace(message))
        {
            return TruncateSummary(message);
        }

        var special = GetSpecialMessageCompact(entry.MessageKey);
        if (special is not null)
        {
            return TruncateSummary(special);
        }

        return TruncateSummary(CompactPrefix(Summary));
    }

    private string CompactImportSummary()
    {
        var result = TryArgument("result", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "completed";
        var operation = !string.IsNullOrWhiteSpace(Operation) ? Operation : "Import";
        return $"{operation} {result}";
    }

    private bool TryArgument(string key, out string value)
    {
        if (entry.Arguments is not null
            && entry.Arguments.TryGetValue(key, out var argument)
            && !string.IsNullOrWhiteSpace(argument?.ToString()))
        {
            value = argument.ToString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string CompactPrefix(string value)
    {
        var separator = value.IndexOfAny([':', '\uFF1A', '\n', '\r']);
        return separator > 0 ? value[..separator].Trim() : value.Trim();
    }

    private static string TruncateSummary(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= MaxCompactSummaryLength
            ? normalized
            : normalized[..(MaxCompactSummaryLength - 1)].TrimEnd() + '\u2026';
    }

    private IReadOnlyList<LogPropertyViewModel> CreateStructuredProperties()
    {
        var properties = entry.StructuredState!
            .Where(static pair => !IsOverviewHiddenKey(pair.Key) && !IsContainer(pair.Value))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new LogPropertyViewModel(HumanizeKey(pair.Key), FormatValue(pair.Value)))
            .ToList();

        if (TryGetImportOverview(out var importOverview))
        {
            properties.Insert(0, new LogPropertyViewModel("Import Entries", importOverview));
        }

        return properties;
    }

    private bool TryGetImportOverview(out string value)
    {
        value = entry.StructuredState is not null
            && entry.StructuredState.TryGetValue("importOverview", out var overview)
            ? overview?.ToString() ?? string.Empty
            : string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string LevelKey(LogLevel level) => level switch
    {
        LogLevel.Warning => "Tool.Log.FilterWarning",
        LogLevel.Error or LogLevel.Critical => "Tool.Log.FilterError",
        _ => "Tool.Log.FilterInformation"
    };

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

    /// <summary>
    /// Keys hidden from the flat overview list: infrastructure keys, the operation tag,
    /// and import details available in the raw representation.
    /// </summary>
    private static bool IsOverviewHiddenKey(string key) =>
        string.Equals(key, "MessageKey", StringComparison.Ordinal) ||
        string.Equals(key, "TechnicalDetail", StringComparison.Ordinal) ||
        string.Equals(key, "Operation", StringComparison.Ordinal) ||
        string.Equals(key, "operation", StringComparison.Ordinal) ||
        string.Equals(key, "Summary", StringComparison.Ordinal) ||
        string.Equals(key, "summary", StringComparison.Ordinal) ||
        string.Equals(key, "severity", StringComparison.Ordinal) ||
        string.Equals(key, "message", StringComparison.Ordinal) ||
        string.Equals(key, "importOverview", StringComparison.Ordinal) ||
        string.Equals(key, "details", StringComparison.Ordinal);

    private static bool IsContainer(object? value) =>
        value is IDictionary or IReadOnlyDictionary<string, object?> or IEnumerable and not string;

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        _ => value.ToString() ?? string.Empty
    };

    private static void AddContextPart(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !parts.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(value.Trim());
        }
    }
}

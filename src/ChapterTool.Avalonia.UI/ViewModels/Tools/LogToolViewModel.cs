using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public enum LogSeverityFilter
{
    All,
    Information,
    Warning,
    Error
}

public sealed record LogFilterOption(LogSeverityFilter Value, string DisplayName);

public sealed record LogPropertyViewModel(string Name, string Value);

public sealed record LogStructuredNodeViewModel(
    string Name,
    string Value,
    IReadOnlyList<LogStructuredNodeViewModel> Children,
    bool IsInitiallyExpanded)
{
    public bool HasChildren => Children.Count > 0;
}

public sealed class LogEntryViewModel : ObservableViewModel
{
    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly ApplicationLogEntry entry;
    private readonly IAppLocalizer localizer;
    private readonly IAppLocalizer contentLocalizer;
    private string? rawText;
    private string? searchableText;

    public LogEntryViewModel(ApplicationLogEntry entry, IAppLocalizer localizer, IAppLocalizer contentLocalizer)
    {
        this.entry = entry;
        this.localizer = localizer;
        this.contentLocalizer = contentLocalizer;
    }

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
            if (HasStructuredTree)
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
    public string RawText => rawText ??= CreateRawText();

    public IReadOnlyList<LogPropertyViewModel> StructuredProperties =>
        entry.StructuredState is not { Count: > 0 }
            ? []
            : entry.StructuredState
                .Where(static pair => !IsOverviewHiddenKey(pair.Key) && !IsContainer(pair.Value))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new LogPropertyViewModel(HumanizeKey(pair.Key), FormatValue(pair.Value)))
                .ToList();

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
        return values
            .Select(pair => CreateNode(pair.Key, pair.Value, depth))
            .ToList();
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
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return CreateNodes(
                readOnlyDictionary
                    .Where(static pair => !IsHiddenStructuredKey(pair.Key))
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal),
                depth);
        }

        if (value is IDictionary dictionary)
        {
            var pairs = new List<KeyValuePair<string, object?>>();
            foreach (DictionaryEntry entry in dictionary)
            {
                pairs.Add(new KeyValuePair<string, object?>(entry.Key?.ToString() ?? string.Empty, entry.Value));
            }

            return CreateNodes(pairs.OrderBy(static pair => pair.Key, StringComparer.Ordinal), depth);
        }

        if (value is IEnumerable enumerable and not string)
        {
            var children = new List<LogStructuredNodeViewModel>();
            var index = 0;
            foreach (var item in enumerable)
            {
                children.Add(CreateNode($"#{++index}", item, depth));
            }

            return children;
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
        value is IDictionary or IReadOnlyDictionary<string, object?> || value is IEnumerable and not string;

    private string CreateRawText()
    {
        var raw = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            ["level"] = entry.Level.ToString(),
            ["operation"] = NullIfEmpty(entry.Operation),
            ["category"] = NullIfEmpty(entry.Category),
            ["eventId"] = entry.EventId,
            ["eventName"] = NullIfEmpty(entry.EventName),
            ["messageKey"] = NullIfEmpty(entry.MessageKey),
            ["message"] = Summary,
            ["technicalDetail"] = NullIfEmpty(entry.TechnicalDetail),
            ["exception"] = NullIfEmpty(entry.ExceptionText),
            ["structuredState"] = NormalizeRawValue(entry.StructuredState, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
        };
        return JsonSerializer.Serialize(raw, RawJsonOptions);
    }

    private static object? NormalizeRawValue(object? value, int depth, HashSet<object> path)
    {
        if (value is null || value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal)
        {
            return value;
        }

        if (value is char or Enum or Guid or Uri or TimeSpan)
        {
            return value.ToString();
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
        }

        if (depth >= 32)
        {
            return "[depth limit]";
        }

        if (!value.GetType().IsValueType && !path.Add(value))
        {
            return "[cycle]";
        }

        try
        {
            if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                return readOnlyDictionary.ToDictionary(
                    static pair => pair.Key,
                    pair => NormalizeRawValue(pair.Value, depth + 1, path),
                    StringComparer.Ordinal);
            }

            if (value is IDictionary dictionary)
            {
                var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry item in dictionary)
                {
                    normalized[item.Key?.ToString() ?? string.Empty] = NormalizeRawValue(item.Value, depth + 1, path);
                }

                return normalized;
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable.Cast<object?>()
                    .Select(item => NormalizeRawValue(item, depth + 1, path))
                    .ToList();
            }

            return value.ToString();
        }
        finally
        {
            if (!value.GetType().IsValueType)
            {
                path.Remove(value);
            }
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        _ => value.ToString() ?? string.Empty
    };
}

public sealed class LogToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IApplicationLogService logService;
    private readonly IAppLocalizer localizer;
    private readonly IAppLocalizer contentLocalizer = new AppLocalizationManager("en-US");
    private readonly IClipboardService? clipboardService;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly List<LogEntryViewModel> entryViewModels = [];
    private IReadOnlyList<LogEntryViewModel> filteredEntries = [];
    private IReadOnlyList<LogFilterOption> filterOptions;
    private LogFilterOption selectedFilter;
    private bool disposed;

    public LogToolViewModel(
        IApplicationLogService logService,
        IAppLocalizer localizer,
        IClipboardService? clipboardService = null)
    {
        ArgumentNullException.ThrowIfNull(logService);
        ArgumentNullException.ThrowIfNull(localizer);
        this.logService = logService;
        this.localizer = localizer;
        this.clipboardService = clipboardService;
        synchronizationContext = SynchronizationContext.Current;
        filterOptions = CreateFilterOptions();
        selectedFilter = filterOptions[0];
        ClearCommand = new UiCommand(
            (_, _) =>
            {
                logService.Clear();
                return ValueTask.CompletedTask;
            },
            _ => entryViewModels.Count > 0);
        CopySummaryCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.Summary, cancellationToken),
            _ => clipboardService is not null && SelectedEntry is not null);
        CopyDetailsCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.RawText, cancellationToken),
            _ => clipboardService is not null && SelectedEntry is not null);
        logService.EntryAdded += OnEntryAdded;
        logService.Cleared += OnCleared;
        localizer.CultureChanged += OnCultureChanged;
        RebuildFromService();
    }

    public IReadOnlyList<LogFilterOption> FilterOptions
    {
        get => filterOptions;
        private set => SetProperty(ref filterOptions, value);
    }

    public LogFilterOption SelectedFilter
    {
        get => selectedFilter;
        set
        {
            if (value is null || !SetProperty(ref selectedFilter, value))
            {
                return;
            }

            RefreshFilteredEntries();
        }
    }

    public string SearchText
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            RefreshFilteredEntries();
        }
    } = string.Empty;

    public IReadOnlyList<LogEntryViewModel> FilteredEntries
    {
        get => filteredEntries;
        private set => SetProperty(ref filteredEntries, value);
    }

    public LogEntryViewModel? SelectedEntry
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedEntry));
            CopySummaryCommand.RaiseCanExecuteChanged();
            CopyDetailsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedEntry => SelectedEntry is not null;

    public bool IsEmpty => FilteredEntries.Count == 0;

    public string EntryCountText => localizer.FormatPositional("Tool.Log.EntryCount", FilteredEntries.Count);

    public UiCommand ClearCommand { get; }

    public UiCommand CopySummaryCommand { get; }

    public UiCommand CopyDetailsCommand { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        logService.EntryAdded -= OnEntryAdded;
        logService.Cleared -= OnCleared;
        localizer.CultureChanged -= OnCultureChanged;
    }

    private void OnEntryAdded(object? sender, ApplicationLogEntry entry)
    {
        if (synchronizationContext is null)
        {
            AppendEntry(entry);
            return;
        }

        synchronizationContext.Post(_ => AppendEntry(entry), null);
    }

    private void OnCleared(object? sender, EventArgs args)
    {
        if (synchronizationContext is null)
        {
            RebuildAfterClear();
            return;
        }

        synchronizationContext.Post(_ => RebuildAfterClear(), null);
    }

    private void OnCultureChanged(object? sender, EventArgs args)
    {
        FilterOptions = CreateFilterOptions();
        SelectedFilter = FilterOptions.First(option => option.Value == selectedFilter.Value);
        foreach (var entry in entryViewModels)
        {
            entry.RefreshLocalizedProperties();
        }

        RefreshFilteredEntries();
    }

    private IReadOnlyList<LogFilterOption> CreateFilterOptions() =>
    [
        new(LogSeverityFilter.All, localizer.GetString("Tool.Log.FilterAll")),
        new(LogSeverityFilter.Information, localizer.GetString("Tool.Log.FilterInformation")),
        new(LogSeverityFilter.Warning, localizer.GetString("Tool.Log.FilterWarning")),
        new(LogSeverityFilter.Error, localizer.GetString("Tool.Log.FilterError"))
    ];

    private void AppendEntry(ApplicationLogEntry entry)
    {
        if (disposed)
        {
            return;
        }

        var snapshot = logService.Entries;
        var appendsEntry = snapshot.Count == entryViewModels.Count + 1
            && PrefixMatches(snapshot, entryViewModels, entryViewModels.Count)
            && ReferenceEquals(snapshot[^1], entry);
        var evictsThenAppends = snapshot.Count == entryViewModels.Count
            && snapshot.Count > 0
            && PrefixMatches(snapshot, entryViewModels, snapshot.Count - 1, existingOffset: 1)
            && ReferenceEquals(snapshot[^1], entry);

        if (!appendsEntry && !evictsThenAppends)
        {
            RebuildFromSnapshot(snapshot);
            return;
        }

        var viewModel = new LogEntryViewModel(entry, localizer, contentLocalizer);
        if (evictsThenAppends)
        {
            entryViewModels.RemoveAt(0);
        }

        entryViewModels.Add(viewModel);
        if (evictsThenAppends)
        {
            RefreshFilteredEntries();
            return;
        }

        if (viewModel.Matches(SelectedFilter.Value) && viewModel.MatchesSearch(SearchText))
        {
            FilteredEntries = [.. filteredEntries, viewModel];
            SelectedEntry ??= viewModel;
            RaiseListStateChanged();
        }
    }

    private void RebuildAfterClear()
    {
        if (!disposed)
        {
            RebuildFromService();
        }
    }

    private void RebuildFromService() => RebuildFromSnapshot(logService.Entries);

    private void RebuildFromSnapshot(IReadOnlyList<ApplicationLogEntry> entries)
    {
        var existing = new Dictionary<ApplicationLogEntry, LogEntryViewModel>(ReferenceEqualityComparer.Instance);
        foreach (var item in entryViewModels)
        {
            existing[item.Entry] = item;
        }

        entryViewModels.Clear();
        foreach (var entry in entries)
        {
            entryViewModels.Add(existing.TryGetValue(entry, out var viewModel)
                ? viewModel
                : new LogEntryViewModel(entry, localizer, contentLocalizer));
        }

        RefreshFilteredEntries();
    }

    private static bool PrefixMatches(
        IReadOnlyList<ApplicationLogEntry> snapshot,
        IReadOnlyList<LogEntryViewModel> existing,
        int count,
        int existingOffset = 0)
    {
        for (var index = 0; index < count; index++)
        {
            if (!ReferenceEquals(snapshot[index], existing[index + existingOffset].Entry))
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshFilteredEntries()
    {
        var previousEntry = SelectedEntry?.Entry;
        FilteredEntries = entryViewModels
            .Where(entry => entry.Matches(SelectedFilter.Value) && entry.MatchesSearch(SearchText))
            .ToList();
        SelectedEntry = FilteredEntries.FirstOrDefault(entry => ReferenceEquals(entry.Entry, previousEntry))
            ?? FilteredEntries.LastOrDefault();
        RaiseListStateChanged();
    }

    private void RaiseListStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EntryCountText));
        ClearCommand.RaiseCanExecuteChanged();
        CopySummaryCommand.RaiseCanExecuteChanged();
        CopyDetailsCommand.RaiseCanExecuteChanged();
    }

    private async ValueTask CopyAsync(string? text, CancellationToken cancellationToken)
    {
        if (clipboardService is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await clipboardService.SetTextAsync(text, cancellationToken);
    }
}

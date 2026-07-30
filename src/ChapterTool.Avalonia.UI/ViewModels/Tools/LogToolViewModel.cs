using System.Collections;
using System.Text;
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
    IReadOnlyList<LogStructuredNodeViewModel> Children)
{
    public bool HasChildren => Children.Count > 0;
}

public sealed class LogEntryViewModel : ObservableViewModel
{
    private readonly ApplicationLogEntry entry;
    private readonly IAppLocalizer localizer;

    public LogEntryViewModel(ApplicationLogEntry entry, IAppLocalizer localizer)
    {
        this.entry = entry;
        this.localizer = localizer;
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
                : localizer.Format(entry.MessageKey, entry.Arguments);
        }
    }

    public string Category => entry.Category ?? string.Empty;

    public string EventName => entry.EventName ?? string.Empty;

    public string Context => string.IsNullOrWhiteSpace(EventName)
        ? Category
        : string.IsNullOrWhiteSpace(Category)
            ? EventName
            : $"{Category} / {EventName}";

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

    public string ExceptionText => entry.ExceptionText?.Trim() ?? string.Empty;

    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionText);

    public IReadOnlyList<LogPropertyViewModel> StructuredProperties =>
        entry.StructuredState is not { Count: > 0 }
            ? []
            : entry.StructuredState
                .Where(static pair => !string.Equals(pair.Key, "MessageKey", StringComparison.Ordinal))
                .Where(static pair => !string.Equals(pair.Key, "TechnicalDetail", StringComparison.Ordinal))
                .Where(static pair => !string.Equals(pair.Key, "severity", StringComparison.Ordinal))
                .Where(static pair => !string.Equals(pair.Key, "message", StringComparison.Ordinal))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new LogPropertyViewModel(pair.Key, FormatValue(pair.Value)))
                .ToList();

    public bool HasStructuredProperties => StructuredProperties.Count > 0;

    public IReadOnlyList<LogStructuredNodeViewModel> StructuredTree =>
        entry.StructuredState is not { Count: > 0 }
            ? []
            : CreateNodes(
                entry.StructuredState
                    .Where(static pair => !IsHiddenStructuredKey(pair.Key))
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

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(LevelText));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(TechnicalDetail));
        OnPropertyChanged(nameof(ExceptionText));
        OnPropertyChanged(nameof(StructuredProperties));
        OnPropertyChanged(nameof(StructuredTree));
        OnPropertyChanged(nameof(HasStructuredTree));
        OnPropertyChanged(nameof(Context));
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
            return new LogStructuredNodeViewModel(name, "[depth limit]", []);
        }

        var children = CreateChildren(value, depth + 1);
        return new LogStructuredNodeViewModel(name, FormatContainer(value, children), children);
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
                children.Add(CreateNode($"[{index++}]", item, depth));
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
            ? $"{{{children.Count} fields}}"
            : $"[{children.Count} items]";
    }

    private static bool IsHiddenStructuredKey(string key) =>
        string.Equals(key, "MessageKey", StringComparison.Ordinal) ||
        string.Equals(key, "TechnicalDetail", StringComparison.Ordinal);

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
    private readonly IClipboardService? clipboardService;
    private readonly SynchronizationContext? synchronizationContext;
    private IReadOnlyList<ApplicationLogEntry> entries = [];
    private IReadOnlyList<LogEntryViewModel> filteredEntries = [];
    private IReadOnlyList<LogFilterOption> filterOptions = [];
    private LogEntryViewModel? selectedEntry;
    private LogFilterOption selectedFilter = null!;
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
                RefreshEntries();
                return ValueTask.CompletedTask;
            },
            _ => entries.Count > 0);
        CopySummaryCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.Summary, cancellationToken),
            _ => clipboardService is not null && SelectedEntry is not null);
        CopyDetailsCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.Details, cancellationToken),
            _ => clipboardService is not null && SelectedEntry?.HasDetails == true);
        RefreshEntries();
        logService.EntryAdded += OnEntryAdded;
        localizer.CultureChanged += OnCultureChanged;
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

    public IReadOnlyList<LogEntryViewModel> FilteredEntries
    {
        get => filteredEntries;
        private set => SetProperty(ref filteredEntries, value);
    }

    public LogEntryViewModel? SelectedEntry
    {
        get => selectedEntry;
        set
        {
            if (!SetProperty(ref selectedEntry, value))
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
        localizer.CultureChanged -= OnCultureChanged;
    }

    private void OnEntryAdded(object? sender, ApplicationLogEntry entry)
    {
        if (synchronizationContext is null)
        {
            RefreshEntries();
            return;
        }

        synchronizationContext.Post(static state => ((LogToolViewModel)state!).RefreshEntries(), this);
    }

    private void OnCultureChanged(object? sender, EventArgs args)
    {
        FilterOptions = CreateFilterOptions();
        SelectedFilter = FilterOptions.First(option => option.Value == selectedFilter.Value);
        foreach (var entry in FilteredEntries)
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

    private void RefreshEntries()
    {
        var previousEntry = SelectedEntry?.Entry;
        entries = logService.Entries;
        var viewModels = entries.Select(entry => new LogEntryViewModel(entry, localizer)).ToList();
        FilteredEntries = viewModels.Where(entry => entry.Matches(SelectedFilter.Value)).ToList();
        SelectedEntry = FilteredEntries.FirstOrDefault(entry => ReferenceEquals(entry.Entry, previousEntry))
            ?? FilteredEntries.LastOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
        ClearCommand?.RaiseCanExecuteChanged();
        CopySummaryCommand?.RaiseCanExecuteChanged();
        CopyDetailsCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshFilteredEntries()
    {
        var previousEntry = SelectedEntry?.Entry;
        var viewModels = entries.Select(entry => new LogEntryViewModel(entry, localizer)).ToList();
        FilteredEntries = viewModels.Where(entry => entry.Matches(SelectedFilter.Value)).ToList();
        SelectedEntry = FilteredEntries.FirstOrDefault(entry => ReferenceEquals(entry.Entry, previousEntry))
            ?? FilteredEntries.LastOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
        CopySummaryCommand?.RaiseCanExecuteChanged();
        CopyDetailsCommand?.RaiseCanExecuteChanged();
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

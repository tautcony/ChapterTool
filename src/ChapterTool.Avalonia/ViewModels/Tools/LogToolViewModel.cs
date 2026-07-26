using System.Text;
using Avalonia.Threading;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Localization;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels.Tools;

public enum LogSeverityFilter
{
    All,
    Information,
    Warning,
    Error
}

public sealed record LogFilterOption(LogSeverityFilter Value, string DisplayName);

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

    public string Summary => entry.MessageKey is null
        ? entry.Message
        : localizer.Format(entry.MessageKey, entry.Arguments);

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
            if (entry.StructuredState is { Count: > 0 })
            {
                Append(builder, string.Join(
                    Environment.NewLine,
                    entry.StructuredState
                        .Where(static pair => !string.Equals(pair.Key, "MessageKey", StringComparison.Ordinal))
                        .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                        .Select(static pair => $"{pair.Key}={pair.Value}")));
            }

            return builder.ToString();
        }
    }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

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
}

public sealed class LogToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IApplicationLogService logService;
    private readonly IAppLocalizer localizer;
    private readonly IClipboardService? clipboardService;
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
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshEntries();
            return;
        }

        Dispatcher.UIThread.Post(RefreshEntries);
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

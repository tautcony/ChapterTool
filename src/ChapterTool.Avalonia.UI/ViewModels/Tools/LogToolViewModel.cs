using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public enum LogSeverityFilter
{
    All,
    Information,
    Warning,
    Error
}

public sealed record LogFilterOption(LogSeverityFilter Value, string DisplayName);

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
        FilteredEntries =
        [
            .. entryViewModels
                .Where(entry => entry.Matches(SelectedFilter.Value) && entry.MatchesSearch(SearchText))
        ];
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

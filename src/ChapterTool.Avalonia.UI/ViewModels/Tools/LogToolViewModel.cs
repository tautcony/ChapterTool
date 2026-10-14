using System.Text.Json;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
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

public sealed record LogExportFormatOption(LogExportFormat Value, string DisplayName);

/// <summary>
/// Projects the bounded application log into a quiet feed and an on-demand
/// inspector. Selection and inspection are deliberately separate states.
/// </summary>
public sealed class LogToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IApplicationLogService logService;
    private readonly IAppLocalizer localizer;
    private readonly IClipboardService? clipboardService;
    private readonly IRuntimeCapabilities? capabilities;
    private readonly IApplicationLogExporter? exporter;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly List<LogEntryViewModel> entryViewModels = [];
    private IReadOnlyList<LogFilterOption> filterOptions;
    private IReadOnlyList<LogExportFormatOption> exportFormatOptions;
    private LogFilterOption selectedFilter;
    private LogExportFormatOption selectedExportFormat;
    private string? statusResourceKey;
    private string? statusArgument;
    private bool disposed;

    public LogToolViewModel(
        IApplicationLogService logService,
        IAppLocalizer localizer,
        IClipboardService? clipboardService = null,
        IApplicationLogExporter? exporter = null,
        IRuntimeCapabilities? capabilities = null,
        IShellService? shellService = null,
        string? logDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(logService);
        ArgumentNullException.ThrowIfNull(localizer);
        this.logService = logService;
        this.localizer = localizer;
        this.clipboardService = clipboardService;
        this.capabilities = capabilities;
        this.exporter = exporter;
        OpenLogFolderCommand = new UiCommand(
            (_, token) => shellService is null || string.IsNullOrWhiteSpace(logDirectory)
                ? ValueTask.CompletedTask
                : shellService.OpenAsync(logDirectory, token),
            _ => shellService is not null && !string.IsNullOrWhiteSpace(logDirectory));
        synchronizationContext = SynchronizationContext.Current;
        filterOptions = CreateFilterOptions();
        exportFormatOptions = CreateExportFormatOptions();
        selectedFilter = filterOptions[0];
        selectedExportFormat = exportFormatOptions[0];

        ClearCommand = new UiCommand((_, _) => ClearAsync(), _ => entryViewModels.Count > 0);
        CopySummaryCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.Summary, cancellationToken),
            _ => CanCopySelected);
        CopyDetailsCommand = new UiCommand(
            async (_, cancellationToken) => await CopyAsync(SelectedEntry?.RawText, cancellationToken),
            _ => CanCopySelected);
        CloseDetailsCommand = new UiCommand((_, _) => CloseDetailsAsync(), _ => IsDetailsOpen);
        OpenDetailsCommand = new UiCommand(
            (parameter, _) => OpenDetailsAsync(parameter),
            parameter => parameter is LogEntryViewModel entry && FilteredEntries.Contains(entry));
        ResetFiltersCommand = new UiCommand((_, _) => ResetFiltersAsync(), _ => HasActiveFilters || HasSearchQuery);
        ExportCommand = new UiCommand(
            (_, cancellationToken) => ExportAsync(cancellationToken),
            _ => CanExport);

        logService.EntryAdded += OnEntryAdded;
        logService.Cleared += OnCleared;
        localizer.CultureChanged += OnCultureChanged;
        RebuildFromSnapshot(logService.Entries);
    }

    public IReadOnlyList<LogFilterOption> FilterOptions
    {
        get => filterOptions;
        private set => SetProperty(ref filterOptions, value);
    }

    public IReadOnlyList<LogExportFormatOption> ExportFormatOptions
    {
        get => exportFormatOptions;
        private set => SetProperty(ref exportFormatOptions, value);
    }

    public LogFilterOption SelectedFilter
    {
        get => selectedFilter;
        set
        {
            if (value is not null && SetProperty(ref selectedFilter, value))
            {
                RefreshProjection();
            }
        }
    }

    public LogExportFormatOption SelectedExportFormat
    {
        get => selectedExportFormat;
        set => SetProperty(ref selectedExportFormat, value);
    }

    public string SearchText
    {
        get;
        set
        {
            var normalized = value ?? string.Empty;
            if (SetProperty(ref field, normalized))
            {
                RefreshProjection();
            }
        }
    } = string.Empty;

    public IReadOnlyList<LogEntryViewModel> FilteredEntries
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>Gets or sets currently highlighted row. It does not open the inspector.</summary>
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
            OnPropertyChanged(nameof(ShowDetails));
            OnPropertyChanged(nameof(CanCopySelected));
            OnPropertyChanged(nameof(HasSecondaryActions));
            RaiseCommandStates();
        }
    }

    /// <summary>Gets a value indicating whether true only while the user is inspecting the selected row.</summary>
    public bool IsDetailsOpen
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ShowDetails));
                CloseDetailsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsExporting
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(HasSecondaryActions));
                ExportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    } = string.Empty;

    public bool HasSelectedEntry => SelectedEntry is not null;

    public bool ShowDetails => IsDetailsOpen && HasSelectedEntry;

    public bool HasRetainedEntries => entryViewModels.Count > 0;

    public bool HasNoResults => HasRetainedEntries && IsEmpty;

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool HasClipboard => clipboardService is not null && (capabilities?.CanWriteClipboard ?? true);

    public bool HasExporter => exporter is not null;

    public bool CanCopySelected => HasClipboard && SelectedEntry is not null;

    public bool CanExport => HasExporter && FilteredEntries.Count > 0 && !IsExporting;

    public bool CanOpenLogFolder => OpenLogFolderCommand.CanExecute(null);

    public bool HasSecondaryActions => HasRetainedEntries || CanCopySelected || CanExport;

    public int ActiveFilterCount => SelectedFilter.Value == LogSeverityFilter.All ? 0 : 1;

    public bool HasActiveFilters => SelectedFilter.Value != LogSeverityFilter.All;

    public bool IsEmpty => FilteredEntries.Count == 0;

    public string EntryCountText => localizer.FormatPositional("Tool.Log.EntryCount", FilteredEntries.Count);

    public UiCommand ClearCommand { get; }

    public UiCommand CopySummaryCommand { get; }

    public UiCommand CopyDetailsCommand { get; }

    public UiCommand CloseDetailsCommand { get; }

    public UiCommand OpenDetailsCommand { get; }

    public UiCommand ResetFiltersCommand { get; }

    public UiCommand ExportCommand { get; }

    public UiCommand OpenLogFolderCommand { get; }

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

    private void OnEntryAdded(object? sender, ApplicationLogEntry entry) => Dispatch(() => AppendEntry(entry));

    private void OnCleared(object? sender, EventArgs args) => Dispatch(RebuildAfterClear);

    private void Dispatch(Action action)
    {
        if (synchronizationContext is null)
        {
            action();
        }
        else
        {
            synchronizationContext.Post(_ => action(), null);
        }
    }

    private void AppendEntry(ApplicationLogEntry entry)
    {
        if (disposed)
        {
            return;
        }

        var retained = logService.Entries.ToHashSet(ReferenceEqualityComparer.Instance);
        var selectedBeforeUpdate = SelectedEntry?.Entry;
        entryViewModels.RemoveAll(item => !retained.Contains(item.Entry));
        if (retained.Contains(entry)
            && entryViewModels.All(item => !ReferenceEquals(item.Entry, entry)))
        {
            entryViewModels.Add(CreateEntry(entry));
        }

        SortEntries();
        if (selectedBeforeUpdate is not null
            && !entryViewModels.Any(item => ReferenceEquals(item.Entry, selectedBeforeUpdate)))
        {
            SelectedEntry = null;
            IsDetailsOpen = false;
        }

        RefreshProjection();
    }

    private async ValueTask ExportAsync(CancellationToken cancellationToken)
    {
        if (exporter is null || IsExporting)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var request = new ApplicationLogExportRequest(
                SelectedExportFormat.Value,
                [.. FilteredEntries.Select(static item => item.Entry)]);
            var result = await exporter.ExportAsync(request, cancellationToken);
            SetExportStatus(result.Succeeded, result.Succeeded ? result.Path : result.Error);
        }
        catch (IOException exception)
        {
            SetExportStatus(false, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            SetExportStatus(false, exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NotSupportedException or InvalidOperationException or System.Security.SecurityException)
        {
            SetExportStatus(false, exception.Message);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private ValueTask ClearAsync()
    {
        logService.Clear();
        return ValueTask.CompletedTask;
    }

    private ValueTask CloseDetailsAsync()
    {
        // Keep the row selected so the same event can be inspected again.
        IsDetailsOpen = false;
        return ValueTask.CompletedTask;
    }

    private ValueTask OpenDetailsAsync(object? parameter)
    {
        if (parameter is LogEntryViewModel entry && FilteredEntries.Contains(entry))
        {
            SelectedEntry = entry;
            IsDetailsOpen = true;
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask ResetFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedFilter = FilterOptions[0];
        return ValueTask.CompletedTask;
    }

    private void RebuildAfterClear()
    {
        if (disposed)
        {
            return;
        }

        entryViewModels.Clear();
        SelectedEntry = null;
        IsDetailsOpen = false;
        RefreshProjection();
    }

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
                : CreateEntry(entry));
        }

        SortEntries();
        RefreshProjection();
    }

    private void OnCultureChanged(object? sender, EventArgs args)
    {
        var severity = SelectedFilter.Value;
        var format = SelectedExportFormat.Value;
        FilterOptions = CreateFilterOptions();
        ExportFormatOptions = CreateExportFormatOptions();
        selectedFilter = FilterOptions.Single(option => option.Value == severity);
        selectedExportFormat = ExportFormatOptions.Single(option => option.Value == format);
        OnPropertyChanged(nameof(SelectedFilter));
        OnPropertyChanged(nameof(SelectedExportFormat));
        if (statusResourceKey is not null)
        {
            StatusText = localizer.FormatPositional(statusResourceKey, statusArgument ?? string.Empty);
        }
        foreach (var entry in entryViewModels)
        {
            entry.RefreshLocalizedProperties();
        }

        RefreshProjection();
    }

    private void RefreshProjection()
    {
        var previousEntry = SelectedEntry?.Entry;
        foreach (var entry in entryViewModels)
        {
            entry.ApplySearchHighlight(SearchText);
        }

        FilteredEntries =
        [
            .. entryViewModels
                .Where(entry => entry.Matches(SelectedFilter.Value))
                .Where(entry => entry.MatchesSearch(SearchText))
                .OrderByDescending(static entry => entry.Entry.Timestamp)
        ];

        var stillVisible = FilteredEntries.FirstOrDefault(entry => ReferenceEquals(entry.Entry, previousEntry));
        if (previousEntry is not null && stillVisible is null)
        {
            SelectedEntry = null;
            IsDetailsOpen = false;
        }

        // Filtering and live updates must not select a replacement entry. Keep
        // an explicit selection only while that same entry remains visible.
        else if (previousEntry is not null && !ReferenceEquals(SelectedEntry, stillVisible))
        {
            SelectedEntry = stillVisible;
        }

        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ActiveFilterCount));
        OnPropertyChanged(nameof(HasSearchQuery));
        RaiseListStateChanged();
    }

    private void RaiseListStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRetainedEntries));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(EntryCountText));
        OnPropertyChanged(nameof(HasSecondaryActions));
        OnPropertyChanged(nameof(CanCopySelected));
        OnPropertyChanged(nameof(CanExport));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        ClearCommand.RaiseCanExecuteChanged();
        CopySummaryCommand.RaiseCanExecuteChanged();
        CopyDetailsCommand.RaiseCanExecuteChanged();
        CloseDetailsCommand.RaiseCanExecuteChanged();
        OpenDetailsCommand.RaiseCanExecuteChanged();
        ResetFiltersCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async ValueTask CopyAsync(string? text, CancellationToken cancellationToken)
    {
        if (clipboardService is not null && !string.IsNullOrWhiteSpace(text))
        {
            await clipboardService.SetTextAsync(text, cancellationToken);
        }
    }

    private LogEntryViewModel CreateEntry(ApplicationLogEntry entry) => new(entry, localizer);

    private void SetExportStatus(bool succeeded, string? value)
    {
        statusResourceKey = succeeded ? "Tool.Log.ExportSucceeded" : "Tool.Log.ExportFailed";
        statusArgument = value ?? string.Empty;
        StatusText = localizer.FormatPositional(statusResourceKey, statusArgument);
    }

    private void SortEntries() => entryViewModels.Sort(static (left, right) => right.Entry.Timestamp.CompareTo(left.Entry.Timestamp));

    private IReadOnlyList<LogFilterOption> CreateFilterOptions() =>
    [
        new(LogSeverityFilter.All, localizer.GetString("Tool.Log.FilterAll")),
        new(LogSeverityFilter.Information, localizer.GetString("Tool.Log.FilterInformation")),
        new(LogSeverityFilter.Warning, localizer.GetString("Tool.Log.FilterWarning")),
        new(LogSeverityFilter.Error, localizer.GetString("Tool.Log.FilterError"))
    ];

    private IReadOnlyList<LogExportFormatOption> CreateExportFormatOptions() =>
    [
        new(LogExportFormat.Json, localizer.GetString("Tool.Log.ExportJson")),
        new(LogExportFormat.Csv, localizer.GetString("Tool.Log.ExportCsv"))
    ];
}

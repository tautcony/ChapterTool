using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class HistoryToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IHistorySessionPort history;
    private readonly IAppLocalizer localizer;
    private readonly EventHandler stateChangedHandler;
    private readonly EventHandler cultureChangedHandler;
    private readonly HashSet<Guid> expandedNodeIds = [];
    private SessionHistorySnapshot? currentSnapshot;
    private IReadOnlyList<HistoryEntryViewModel> visibleEntries = [];
    private IReadOnlyList<HistoryEntryViewModel> treeRoots = [];
    private IReadOnlyList<HistoryRedoChoice> redoChoices = [];
    private HistoryRedoChoice? selectedRedoChoice;
    private bool isNarrowLayout;
    private int narrowPageIndex;
    private CancellationTokenSource? detailCancellation;
    private HistoryEntryViewModel? selectedEntry;
    private HistoryNodeDetails? details;
    private bool isLoadingDetails;
    private string? detailError;
    private long detailGeneration;
    private Guid? selectedRootId;
    private string? navigationStatus;
    private bool disposed;
    private IReadOnlyList<HistoryFieldRow> fieldRows = [];
    private IReadOnlyList<HistoryParameterRow> parameterRows = [];
    private IReadOnlyList<HistoryRootChapterRow> rootChapterRows = [];
    private string selectedChangeFilter = "All";

    public HistoryToolViewModel(IHistorySessionPort history, IAppLocalizer localizer, Action? close = null)
    {
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        selectedChangeFilter = localizer.GetString("History.Filter.All");
        stateChangedHandler = (_, _) =>
        {
            if (!disposed)
            {
                RefreshState();
            }
        };
        cultureChangedHandler = (_, _) =>
        {
            if (disposed)
            {
                return;
            }
            OnPropertyChanged(nameof(SessionLifetimeText));
            OnPropertyChanged(nameof(DetailErrorText));
            OnPropertyChanged(nameof(NavigationStatus));
            RebuildEntries();
            RebuildInspectorRows();
            RebuildRootChapterRows();
            RebuildRedoChoices();
            OnPropertyChanged(nameof(RedoChoices));
            OnPropertyChanged(nameof(HasAlternateRedoChoices));
            OnPropertyChanged(nameof(EffectSummary));
            OnPropertyChanged(nameof(HasEffectSummary));
            OnPropertyChanged(nameof(ChangeFilters));
            OnPropertyChanged(nameof(SelectedEntryDescription));
            OnPropertyChanged(nameof(HasSelectedEntryDescription));
            OnPropertyChanged(nameof(SelectedPublishedAtText));
            OnPropertyChanged(nameof(HasPublishedAtText));
            selectedChangeFilter = localizer.GetString("History.Filter.All");
            OnPropertyChanged(nameof(SelectedChangeFilter));
            OnPropertyChanged(nameof(Entries));
        };
        history.StateChanged += stateChangedHandler;
        localizer.CultureChanged += cultureChangedHandler;
        currentSnapshot = history.Snapshot;
        RebuildRedoChoices();
        CloseCommand = new UiCommand((_, _) =>
        {
            close?.Invoke();
            return ValueTask.CompletedTask;
        });
        SelectCommand = new UiCommand((parameter, _) =>
        {
            if (parameter is HistoryEntryViewModel entry)
            {
                SelectedEntry = entry;
            }
            return ValueTask.CompletedTask;
        });
        RestoreCommand = new UiCommand(RestoreAsync, CanRestore);
        ChooseRedoBranchCommand = new UiCommand(ChooseRedoBranchAsync, CanChooseRedoBranch);
        RetryCommand = new UiCommand(async (_, _) => await RetryDetailsAsync(), _ => HasDetailError);
        ToggleExpansionCommand = new UiCommand((parameter, _) =>
        {
            if (parameter is Guid nodeId)
            {
                ToggleExpansion(nodeId);
            }
            return ValueTask.CompletedTask;
        }, parameter => parameter is Guid nodeId && currentSnapshot?.Nodes.Any(node => node.Id == nodeId && !node.ChildIds.IsEmpty) == true);
        LocateCurrentCommand = new UiCommand((_, _) =>
        {
            SelectCurrentNode();
            return ValueTask.CompletedTask;
        });
        SelectCurrentNode();
        SelectedEntry = Entries.FirstOrDefault(entry => entry.IsCurrent) ?? Entries.FirstOrDefault();
    }

    public IReadOnlyList<HistoryEntryViewModel> Entries => visibleEntries;

    public IReadOnlyList<HistoryEntryViewModel> TreeRoots => treeRoots;

    public IReadOnlyList<HistoryRedoChoice> RedoChoices => redoChoices;

    public HistoryRedoChoice? SelectedRedoChoice
    {
        get => selectedRedoChoice;
        set
        {
            if (SetProperty(ref selectedRedoChoice, value))
            {
                ChooseRedoBranchCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasAlternateRedoChoices => RedoChoices.Count > 1;

    public bool IsNarrowLayout
    {
        get => isNarrowLayout;
        private set
        {
            if (SetProperty(ref isNarrowLayout, value))
            {
                OnPropertyChanged(nameof(IsWideLayout));
            }
        }
    }

    public bool IsWideLayout => !IsNarrowLayout;

    public int NarrowPageIndex
    {
        get => narrowPageIndex;
        set => SetProperty(ref narrowPageIndex, value);
    }

    public HistoryEntryViewModel? SelectedEntry
    {
        get => selectedEntry;
        set
        {
            if (!SetProperty(ref selectedEntry, value))
            {
                return;
            }
            _ = LoadDetailsAsync(value?.Id);
            OnPropertyChanged(nameof(SelectedEntryDescription));
            OnPropertyChanged(nameof(HasSelectedEntryDescription));
            OnPropertyChanged(nameof(SelectedPublishedAtText));
            OnPropertyChanged(nameof(HasPublishedAtText));
            RestoreCommand.RaiseCanExecuteChanged();
        }
    }

    public HistoryNodeDetails? Details
    {
        get => details;
        private set
        {
            if (SetProperty(ref details, value))
            {
                OnPropertyChanged(nameof(IsRootDetails));
                OnPropertyChanged(nameof(IsHistoryDetails));
                RebuildInspectorRows();
                RebuildRootChapterRows();
                OnPropertyChanged(nameof(EffectSummary));
                OnPropertyChanged(nameof(HasEffectSummary));
                OnPropertyChanged(nameof(HasParameters));
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool IsLoadingDetails
    {
        get => isLoadingDetails;
        private set => SetProperty(ref isLoadingDetails, value);
    }

    public string? DetailError
    {
        get => detailError;
        private set
        {
            if (SetProperty(ref detailError, value))
            {
                OnPropertyChanged(nameof(HasDetailError));
                RetryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DetailErrorText => DetailError switch
    {
        "ResourceFailure" => localizer.GetString("History.DetailResourceFailure"),
        "NotFound" => localizer.GetString("History.DetailNotFound"),
        "Ended" => localizer.GetString("History.DetailEnded"),
        _ => localizer.GetString("History.DetailFailed")
    };

    public string? NavigationStatus
    {
        get => navigationStatus;
        private set
        {
            if (SetProperty(ref navigationStatus, value))
            {
                OnPropertyChanged(nameof(HasNavigationStatus));
            }
        }
    }

    public bool HasDetailError => !string.IsNullOrWhiteSpace(DetailError);

    public bool HasSelection => SelectedEntry is not null;

    public bool HasSelectedEntryDescription => !string.IsNullOrWhiteSpace(SelectedEntryDescription);

    public bool HasNavigationStatus => !string.IsNullOrWhiteSpace(NavigationStatus);

    public bool HasDeltas => VisibleChanges.Any(static row => !string.IsNullOrWhiteSpace(row.DeltaText));

    public bool IsRootDetails => Details?.RootDocument is not null;

    public bool IsHistoryDetails => Details is not null && !IsRootDetails;

    public IReadOnlyList<string> ChangeFilters => [localizer.GetString("History.Filter.All"), localizer.GetString("History.Filter.Added"), localizer.GetString("History.Filter.Removed"), localizer.GetString("History.Filter.Modified")];

    public string? SelectedEntryDescription => SelectedEntry?.Operation is null || SelectedEntry.Operation.Kind == "generic" ? SelectedEntry?.Description : null;

    public string? SelectedPublishedAtText => SelectedEntry?.PublishedAt?.ToLocalTime().ToString("g", System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName));

    public bool HasPublishedAtText => !string.IsNullOrWhiteSpace(SelectedPublishedAtText);

    public IReadOnlyList<HistoryFieldRow> VisibleChanges => SelectedChangeFilter switch
    {
        var value when value == localizer.GetString("History.Filter.Added") => fieldRows.Where(static row => !row.Before.IsPresent && row.After.IsPresent).ToArray(),
        var value when value == localizer.GetString("History.Filter.Removed") => fieldRows.Where(static row => row.Before.IsPresent && !row.After.IsPresent).ToArray(),
        var value when value == localizer.GetString("History.Filter.Modified") => fieldRows.Where(static row => row.Before.IsPresent && row.After.IsPresent).ToArray(),
        _ => fieldRows
    };

    public bool HasParameters => Parameters.Count > 0;

    public bool HasChanges => fieldRows.Count > 0;

    public bool HasVisibleChanges => VisibleChanges.Count > 0;

    public bool HasNoVisibleChanges => HasChanges && !HasVisibleChanges;

    public bool HasRootChapterRows => RootChapterRows.Count > 0;

    public bool HasEffectSummary => !string.IsNullOrWhiteSpace(EffectSummary);

    public string EffectSummary
    {
        get
        {
            var summary = Details?.Summary;
            if (summary is null)
            {
                return string.Empty;
            }
            var items = new List<string>();
            Add(summary.AddedChapters, "History.Added");
            Add(summary.RemovedChapters, "History.Removed");
            Add(summary.ModifiedChapters, "History.Modified");
            Add(summary.ChangedTracks, "History.ChangedTracks");
            Add(summary.ChangedSegments, "History.ChangedSegments");
            return string.Join(" · ", items);

            void Add(int count, string key)
            {
                if (count > 0)
                {
                    items.Add($"{count} {localizer.GetString(key)}");
                }
            }
        }
    }

    public IReadOnlyList<HistoryParameterRow> Parameters => parameterRows;

    public IReadOnlyList<HistoryRootChapterRow> RootChapterRows => rootChapterRows;

    public string SelectedChangeFilter
    {
        get => selectedChangeFilter;
        set
        {
            if (SetProperty(ref selectedChangeFilter, value))
            {
                OnPropertyChanged(nameof(VisibleChanges));
                OnPropertyChanged(nameof(HasDeltas));
                OnPropertyChanged(nameof(HasVisibleChanges));
                OnPropertyChanged(nameof(HasNoVisibleChanges));
            }
        }
    }

    public bool HasPendingPreview => history.HasPendingPreview;

    public UiCommand SelectCommand { get; }

    public UiCommand RetryCommand { get; }

    public UiCommand ToggleExpansionCommand { get; }

    public UiCommand LocateCurrentCommand { get; }

    public UiCommand RestoreCommand { get; }

    public UiCommand ChooseRedoBranchCommand { get; }

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    public string UndoDescription => history.UndoDescription;

    public string RedoDescription => history.RedoDescription;

    public string SessionLifetimeText => localizer.GetString("History.SessionLifetime");

    public UiCommand UndoCommand => history.UndoCommand;

    public UiCommand RedoCommand => history.RedoCommand;

    public UiCommand NavigateCommand => history.NavigateCommand;

    public UiCommand CloseCommand { get; }

    public async Task RetryDetailsAsync()
    {
        await LoadDetailsAsync(SelectedEntry?.Id);
    }

    public void UpdateAvailableWidth(double width) => IsNarrowLayout = width < 800;

    public void ShowDetailsPage() => NarrowPageIndex = 1;

    public void ShowHistoryPage() => NarrowPageIndex = 0;

    public void Dispose()
    {
        disposed = true;
        history.StateChanged -= stateChangedHandler;
        localizer.CultureChanged -= cultureChangedHandler;
        detailCancellation?.Cancel();
        detailCancellation?.Dispose();
        detailGeneration++;
        selectedEntry = null;
        details = null;
        visibleEntries = [];
        treeRoots = [];
        fieldRows = [];
        parameterRows = [];
        rootChapterRows = [];
        expandedNodeIds.Clear();
        OnPropertyChanged(nameof(SelectedEntry));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Entries));
        OnPropertyChanged(nameof(TreeRoots));
        OnPropertyChanged(nameof(VisibleChanges));
        OnPropertyChanged(nameof(Parameters));
        OnPropertyChanged(nameof(RootChapterRows));
    }

    private void RefreshState()
    {
        if (disposed)
        {
            return;
        }
        var previousSnapshot = currentSnapshot;
        var previousSelection = selectedEntry?.Id;
        currentSnapshot = history.Snapshot;
        RebuildEntries();
        RebuildRedoChoices();
        OnPropertyChanged(nameof(Entries));
        OnPropertyChanged(nameof(RedoChoices));
        OnPropertyChanged(nameof(HasAlternateRedoChoices));
        OnPropertyChanged(nameof(HasPendingPreview));
        var snapshot = currentSnapshot;
        if (snapshot is null || history.IsEnded)
        {
            detailGeneration++;
            detailCancellation?.Cancel();
            SelectedEntry = null;
            Details = null;
            DetailError = null;
        }
        else if (previousSnapshot is null || previousSnapshot.RootId != snapshot.RootId || previousSnapshot.CursorId != snapshot.CursorId)
        {
            selectedRootId = snapshot.RootId;
            ExpandCurrentPath(snapshot);
            RebuildEntries();
            SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == snapshot.CursorId);
        }
        else if (previousSelection is Guid selectedId)
        {
            selectedRootId = snapshot.RootId;
            RebuildEntries();
            SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == selectedId);
        }
        NavigationStatus = string.IsNullOrWhiteSpace(history.NavigationStatus) ? null : history.NavigationStatus;
        RestoreCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }

    private async Task LoadDetailsAsync(Guid? nodeId)
    {
        var generation = ++detailGeneration;
        var rootId = currentSnapshot?.RootId;
        selectedRootId = rootId;
        detailCancellation?.Cancel();
        detailCancellation?.Dispose();
        detailCancellation = new CancellationTokenSource();
        var request = detailCancellation;
        Details = null;
        DetailError = null;
        if (nodeId is null)
        {
            IsLoadingDetails = false;
            return;
        }
        IsLoadingDetails = true;
        try
        {
            var result = await history.InspectAsync(nodeId.Value, request.Token);
            if (request.IsCancellationRequested || generation != detailGeneration || SelectedEntry?.Id != nodeId
                || rootId is null || history.Snapshot?.RootId != rootId || history.IsEnded)
            {
                return;
            }
            if (result.Kind == HistoryInspectionOutcomeKind.Available)
            {
                Details = result.Details;
            }
            else
            {
                DetailError = result.Kind switch
                {
                    HistoryInspectionOutcomeKind.NotFound => "NotFound",
                    HistoryInspectionOutcomeKind.Ended => "Ended",
                    HistoryInspectionOutcomeKind.ResourceFailure => "ResourceFailure",
                    _ => "Failed"
                };
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (OutOfMemoryException)
        {
            if (!request.IsCancellationRequested)
            {
                DetailError = "ResourceFailure";
            }
        }
        catch (Exception)
        {
            if (!request.IsCancellationRequested && generation == detailGeneration)
            {
                DetailError = "Failed";
            }
        }
        finally
        {
            if (ReferenceEquals(detailCancellation, request))
            {
                IsLoadingDetails = false;
            }
        }
    }

    private bool CanRestore(object? parameter) =>
        parameter is Guid nodeId
        && !history.IsEnded
        && nodeId != currentSnapshot?.CursorId
        && currentSnapshot?.Nodes.Any(node => node.Id == nodeId) == true;

    private bool CanChooseRedoBranch(object? parameter) =>
        parameter is Guid nodeId
        && !history.IsEnded
        && currentSnapshot?.Nodes.FirstOrDefault(node => node.Id == currentSnapshot.CursorId)?.ChildIds.Contains(nodeId) == true;

    private void ToggleExpansion(Guid nodeId)
    {
        var node = visibleEntries.FirstOrDefault(entry => entry.Id == nodeId);
        if (node is null)
        {
            return;
        }
        node.IsExpanded = !node.IsExpanded;
        if (!node.IsExpanded && selectedEntry is { } selected && IsDescendant(selected.Id, nodeId))
        {
            SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == nodeId);
        }
        OnPropertyChanged(nameof(Entries));
    }

    private void SelectCurrentNode()
    {
        if (currentSnapshot is not { } snapshot)
        {
            return;
        }
        ExpandCurrentPath(snapshot);
        RebuildEntries();
        OnPropertyChanged(nameof(Entries));
        SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == snapshot.CursorId);
    }

    private void RebuildEntries()
    {
        var previousNodes = visibleEntries.ToDictionary(static entry => entry.Id);
        var (roots, projected) = HistoryEntryViewModel.CreateTree(currentSnapshot, previousNodes, expandedNodeIds, LocalizeHistoryTitle);
        treeRoots = roots;
        visibleEntries = projected;
        OnPropertyChanged(nameof(TreeRoots));
        if (selectedEntry is { } selected && visibleEntries.FirstOrDefault(entry => entry.Id == selected.Id) is { } replacement && !ReferenceEquals(selected, replacement))
        {
            selectedEntry = replacement;
            OnPropertyChanged(nameof(SelectedEntry));
            OnPropertyChanged(nameof(SelectedEntryDescription));
            OnPropertyChanged(nameof(SelectedPublishedAtText));
            OnPropertyChanged(nameof(HasPublishedAtText));
            RestoreCommand?.RaiseCanExecuteChanged();
        }
    }

    private void RebuildRedoChoices()
    {
        var oldSelection = selectedRedoChoice?.Id;
        var nodes = currentSnapshot?.Nodes.ToDictionary(node => node.Id);
        if (currentSnapshot is null || nodes is null || !nodes.TryGetValue(currentSnapshot.CursorId, out var current))
        {
            redoChoices = [];
            SelectedRedoChoice = null;
            return;
        }

        redoChoices = current.ChildIds.Select(childId => nodes.TryGetValue(childId, out var child)
            ? new HistoryRedoChoice(child.Id, LocalizeHistoryTitle(child), child.Description, current.PreferredChildId == child.Id)
            : null).Where(choice => choice is not null).Cast<HistoryRedoChoice>().ToArray();
        SelectedRedoChoice = redoChoices.FirstOrDefault(choice => choice.Id == oldSelection)
            ?? redoChoices.FirstOrDefault(choice => choice.IsPreferred)
            ?? redoChoices.FirstOrDefault();
    }

    private async ValueTask ChooseRedoBranchAsync(object? parameter, CancellationToken cancellationToken)
    {
        var nodeId = parameter is Guid selectedId ? selectedId : SelectedRedoChoice?.Id;
        if (nodeId is null || !CanChooseRedoBranch(nodeId))
        {
            return;
        }
        await RestoreAsync(nodeId.Value, cancellationToken);
    }

    private string LocalizeHistoryTitle(SessionHistoryNodeSnapshot node)
    {
        var resourceKey = node.Operation?.Kind switch
        {
            "load" => "History.Operation.Load",
            "chapter.cell-edit" => "History.Operation.Edit",
            "chapter.delete" => "History.Operation.Delete",
            "chapter.insert" => "History.Operation.Insert",
            "chapter.frame-shift" => "History.Operation.FrameShift",
            "chapter.frame-rate-change" => "History.Operation.FrameRateChange",
            "expression.apply" => "History.Operation.Expression",
            "template.apply" => "History.Operation.Template",
            "chapter.options-apply" => "History.Operation.Options",
            "workspace.merge" => "History.Operation.Merge",
            "workspace.split" => "History.Operation.Split",
            "workspace.append" => "History.Operation.Append",
            _ => "History.Operation.Generic"
        };
        var title = localizer.GetString(resourceKey);
        if (node.Operation?.Kind == "chapter.cell-edit")
        {
            var field = node.Operation.Parameters.FirstOrDefault(static parameter => parameter.Name == "field")?.Value;
            if (!string.IsNullOrWhiteSpace(field))
            {
                title = $"{title} · {localizer.GetString($"History.Enum.Field.{field}")}";
            }
        }
        return title;
    }

    private void RebuildInspectorRows()
    {
        fieldRows = Details?.Changes.Select(change => new HistoryFieldRow(
            change.Group,
            change.ObjectId,
            localizer.GetString($"History.Field.{change.Field}"),
            FormatFieldContext(change),
            change.Before,
            change.After,
            FormatValue(change.Before, change.Field),
            FormatValue(change.After, change.Field),
            FormatDelta(change))).ToArray() ?? [];
        var operationKind = Details?.Operation?.Kind;
        var cellEditField = Details?.Operation?.Parameters
            .FirstOrDefault(static parameter => parameter.Name == "field")?.Value;
        parameterRows = Details?.Operation?.Parameters
            .Where(parameter => parameter.Name is not "chapterId" and not "chapterIds" and not "trackId" and not "trackIds" and not "targetChapterIds"
                && !(operationKind == "chapter.cell-edit"
                    && (parameter.Name is "field" or "value" or "trackName"
                        || (parameter.Name == "displayFps" && cellEditField is not ("Frame" or "StartTime")))))
            .Select(parameter => new HistoryParameterRow(
                localizer.GetString($"History.Parameter.{parameter.Name}"),
                FormatParameter(parameter),
                parameter.ValueKind)).ToArray() ?? [];
        OnPropertyChanged(nameof(VisibleChanges));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasVisibleChanges));
        OnPropertyChanged(nameof(HasNoVisibleChanges));
        OnPropertyChanged(nameof(HasDeltas));
        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(Parameters));
    }

    private string FormatFieldContext(HistoryFieldChange change)
    {
        var context = change.Group == "Chapter"
            ? localizer.FormatPositional("History.ChapterContext", change.Context ?? string.Empty, change.ObjectLabel)
            : change.Context is null ? change.ObjectLabel : $"{change.Context} · {change.ObjectLabel}";
        if (change.Field is not ("StartTicks" or "EndTicks" or "FramesInfo") ||
            (change.BeforeFrameRate is null && change.AfterFrameRate is null))
        {
            return context;
        }

        var oldRate = change.BeforeFrameRate is { } before ? FormatFrameRate(before) : localizer.GetString("History.Presence.Absent");
        var newRate = change.AfterFrameRate is { } after ? FormatFrameRate(after) : localizer.GetString("History.Presence.Absent");
        return $"{context} · {localizer.FormatPositional("History.FrameRateContext", oldRate, newRate)}";
    }

    private string FormatFrameRate(ChapterFrameRate frameRate)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName);
        return frameRate.Denominator == 1
            ? string.Concat(frameRate.Numerator.ToString(culture), " ", localizer.GetString("History.Unit.Fps"))
            : string.Concat(frameRate.Numerator.ToString(culture), "/", frameRate.Denominator.ToString(culture), " ", localizer.GetString("History.Unit.Fps"));
    }

    private void RebuildRootChapterRows()
    {
        rootChapterRows = Details?.RootDocument is { } document
            ? new HistoryRootChapterRows(document.Tracks, FormatTimeSpan)
            : [];
        OnPropertyChanged(nameof(RootChapterRows));
        OnPropertyChanged(nameof(HasRootChapterRows));
    }

    private string FormatValue(HistoryFieldValue value, string field)
    {
        if (!value.IsPresent)
        {
            return localizer.GetString("History.Presence.Absent");
        }
        if (value.Value is null)
        {
            return localizer.GetString("History.Presence.Null");
        }
        if (value.Value is string text && text.Length == 0)
        {
            return localizer.GetString("History.Presence.Empty");
        }
        if (value.Value is long ticks && field is "StartTicks" or "EndTicks")
        {
            return FormatTimeSpan(TimeSpan.FromTicks(ticks));
        }
        if (value.Value is ChapterDuration duration)
        {
            return duration.IsKnown ? FormatTimeSpan(TimeSpan.FromTicks(duration.Ticks)) : localizer.GetString("History.Presence.Unknown");
        }
        if (value.Value is ChapterFrameRate frameRate)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName);
            return frameRate.Denominator == 1
                ? string.Concat(frameRate.Numerator.ToString(culture), " ", localizer.GetString("History.Unit.Fps"))
                : string.Concat(frameRate.Numerator.ToString(culture), "/", frameRate.Denominator.ToString(culture), " ", localizer.GetString("History.Unit.Fps"));
        }
        if (value.Value is HistoryTrackReference trackReference)
        {
            return string.IsNullOrEmpty(trackReference.Label)
                ? localizer.GetString("History.Track.Unknown")
                : trackReference.Label;
        }
        if (value.Value is ChapterImportFormat format)
        {
            return localizer.GetString($"History.Enum.ImportFormat.{format}");
        }
        if (value.Value is ChapterKind kind)
        {
            return localizer.GetString($"History.Enum.ChapterKind.{kind}");
        }
        if (value.Value is FrameAccuracy accuracy)
        {
            return localizer.GetString(accuracy == FrameAccuracy.Neutral
                ? "Expression.FrameAccuracy.NotCalculated"
                : $"Expression.FrameAccuracy.{accuracy}");
        }
        if (value.Value is System.Collections.IEnumerable sequence && value.Value is not string)
        {
            var items = new List<string>();
            foreach (var item in sequence)
            {
                items.Add(item is HistoryOrderItem orderItem
                    ? orderItem.Label
                    : Convert.ToString(item, System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName)) ?? string.Empty);
            }
            return string.Join(", ", items);
        }
        return value.Value is IFormattable formattable
            ? formattable.ToString(null, System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName)) ?? string.Empty
            : value.Value.ToString() ?? string.Empty;
    }

    private string? FormatDelta(HistoryFieldChange change)
    {
        if (!change.Before.IsPresent || !change.After.IsPresent)
        {
            return null;
        }
        if (change.Before.Value is long before && change.After.Value is long after && change.Field is "StartTicks" or "EndTicks")
        {
            return FormatTimeSpan(TimeSpan.FromTicks(checked(after - before)), signed: true);
        }
        if (change.Before.Value is ChapterDuration previous && change.After.Value is ChapterDuration next && previous.IsKnown && next.IsKnown)
        {
            return FormatTimeSpan(TimeSpan.FromTicks(checked(next.Ticks - previous.Ticks)), signed: true);
        }
        return null;
    }

    private static string FormatTimeSpan(TimeSpan value, bool signed = false)
    {
        var prefix = signed && value > TimeSpan.Zero ? "+" : string.Empty;
        var absolute = value.Duration();
        var body = absolute.TotalDays >= 1
            ? $"{(int)absolute.TotalDays}.{absolute:hh\\:mm\\:ss\\.fffffff}"
            : absolute.ToString(@"hh\:mm\:ss\.fffffff", System.Globalization.CultureInfo.InvariantCulture);
        return string.Concat(value < TimeSpan.Zero ? "−" : prefix, body);
    }

    private string FormatParameter(HistoryOperationParameter parameter)
    {
        if (parameter.Value is null)
        {
            return localizer.GetString("History.Presence.Null");
        }
        if (parameter.Name == "field")
        {
            return localizer.GetString($"History.Enum.Field.{parameter.Value}");
        }
        if (parameter.ValueKind == "boolean" && bool.TryParse(parameter.Value, out var boolean))
        {
            return localizer.GetString(boolean ? "History.Boolean.True" : "History.Boolean.False");
        }
        if (parameter.ValueKind == "integer" && long.TryParse(parameter.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer))
        {
            return integer.ToString(System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName));
        }
        if (parameter.ValueKind == "decimal" && decimal.TryParse(parameter.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue.ToString(System.Globalization.CultureInfo.GetCultureInfo(localizer.CurrentCultureName));
        }
        if (parameter.Name is "deleteTiming" or "frameDisplay")
        {
            return localizer.GetString($"History.Enum.{parameter.Name}.{parameter.Value}");
        }
        return parameter.Value;
    }

    private void ExpandCurrentPath(ChapterTool.Core.Session.SessionHistorySnapshot snapshot)
    {
        var nodes = snapshot.Nodes.ToDictionary(node => node.Id);
        var currentId = snapshot.CursorId;
        while (nodes.TryGetValue(currentId, out var node))
        {
            if (node.ParentId is not Guid parentId)
            {
                break;
            }
            expandedNodeIds.Add(parentId);
            if (visibleEntries.FirstOrDefault(entry => entry.Id == parentId) is { } existing)
            {
                existing.IsExpanded = true;
            }
            currentId = parentId;
        }
    }

    private bool IsDescendant(Guid candidateId, Guid ancestorId)
    {
        var nodes = history.Snapshot?.Nodes.ToDictionary(node => node.Id);
        if (nodes is null)
        {
            return false;
        }
        var currentId = candidateId;
        while (nodes.TryGetValue(currentId, out var node) && node.ParentId is Guid parentId)
        {
            if (parentId == ancestorId)
            {
                return true;
            }
            currentId = parentId;
        }
        return false;
    }

    private async ValueTask RestoreAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is not Guid nodeId || !CanRestore(nodeId))
        {
            return;
        }
        var oldCursor = history.Snapshot?.CursorId;
        try
        {
            await history.NavigateCommand.ExecuteAsync(nodeId, cancellationToken);
            var current = history.Snapshot?.CursorId;
            if (current != oldCursor)
            {
                SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == current);
                NavigationStatus = localizer.GetString("History.RestoreApplied");
            }
            else
            {
                NavigationStatus = string.IsNullOrWhiteSpace(history.NavigationStatus)
                    ? localizer.GetString("History.NavigationFailed")
                    : history.NavigationStatus;
            }
        }
        catch (Exception)
        {
            NavigationStatus = localizer.GetString("History.NavigationFailed");
        }
    }
}

public sealed record HistoryRedoChoice(Guid Id, string Title, string Description, bool IsPreferred);

public sealed record HistoryFieldRow(string Group, string ObjectId, string Field, string ObjectLabel, HistoryFieldValue Before,
    HistoryFieldValue After, string BeforeText, string AfterText, string? DeltaText);

public sealed record HistoryParameterRow(string Name, string? Value, string ValueKind);

public sealed record HistoryRootChapterRow(string TrackName, int DisplayNumber, string Name, string StartTime, string FramesInfo);

internal sealed class HistoryRootChapterRows : IReadOnlyList<HistoryRootChapterRow>
{
    private readonly IReadOnlyList<EditableChapterTrack> tracks;
    private readonly Func<TimeSpan, bool, string> formatTimeSpan;
    private readonly int[] chapterOffsets;

    public HistoryRootChapterRows(IReadOnlyList<EditableChapterTrack> tracks, Func<TimeSpan, bool, string> formatTimeSpan)
    {
        this.tracks = tracks;
        this.formatTimeSpan = formatTimeSpan;
        chapterOffsets = new int[tracks.Count + 1];
        for (var index = 0; index < tracks.Count; index++)
        {
            chapterOffsets[index + 1] = chapterOffsets[index] + tracks[index].Chapters.Length;
        }
    }

    public int Count => chapterOffsets[^1];

    public HistoryRootChapterRow this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var trackIndex = Array.BinarySearch(chapterOffsets, index + 1);
            trackIndex = trackIndex >= 0 ? trackIndex - 1 : ~trackIndex - 1;
            var chapter = tracks[trackIndex].Chapters[index - chapterOffsets[trackIndex]];
            return new HistoryRootChapterRow(tracks[trackIndex].Name, chapter.DisplayNumber, chapter.Name,
                formatTimeSpan(TimeSpan.FromTicks(chapter.StartTicks), false), chapter.FramesInfo);
        }
    }

    public IEnumerator<HistoryRootChapterRow> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

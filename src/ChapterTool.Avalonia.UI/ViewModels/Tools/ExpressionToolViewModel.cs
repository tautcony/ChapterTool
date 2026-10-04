using Avalonia.Threading;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ExpressionToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IExpressionSessionPort expressionSession;
    private readonly IChapterContentOperationPort? contentOperations;
    private readonly IFilePickerService? filePicker;
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private ChapterContentPreview? pendingPreview;
    private bool settingExpressionFromPreset;
    private bool isPreviewScheduled;
    private int selectedPresetIndex = -1;
    private string expression = string.Empty;
    private string expressionSourceName = string.Empty;
    private string statusText = string.Empty;
    private string previewSummary = string.Empty;

    public ExpressionToolViewModel(
        IExpressionSessionPort expressionSession,
        IFilePickerService? filePicker = null,
        IExpressionAuthoringService? expressionAuthoringService = null,
        Func<Exception, ValueTask>? errorHandler = null,
        IChapterContentOperationPort? contentOperations = null)
    {
        this.expressionSession = expressionSession;
        this.contentOperations = contentOperations;
        this.filePicker = filePicker;
        ExpressionAuthoringService = expressionAuthoringService;
        expression = expressionSession.Expression;
        expressionSourceName = expressionSession.ExpressionSourceName;
        Presets =
        [
            .. expressionSession.ExpressionPresets.Select(static preset =>
                new ExpressionPresetViewModel(preset.Id, preset.DisplayName, preset.Description, preset.ScriptText))
        ];
        selectedPresetIndex = Presets.ToList().FindIndex(preset =>
            string.Equals(preset.Id, expressionSession.ExpressionPresetId, StringComparison.Ordinal));
        previewTimer.Tick += OnPreviewTimerTick;

        BrowseScriptCommand = new UiCommand(async (_, token) => await BrowseScriptAsync(token), _ => this.filePicker is not null)
        {
            ErrorHandler = errorHandler
        };

        // Retain this command for programmatic callers. The view refreshes automatically as the draft changes.
        ApplyCommand = new UiCommand((_, _) =>
        {
            RefreshPreviewNow();
            return ValueTask.CompletedTask;
        }, _ => contentOperations is not null)
        {
            ErrorHandler = errorHandler
        };
        ConfirmApplyCommand = new UiCommand(ApplyPreviewAsync, _ => CanApplyPreview)
        {
            ErrorHandler = errorHandler
        };
        CancelPreviewCommand = new UiCommand((_, _) =>
        {
            var hadPreview = IsPreviewPending;
            previewTimer.Stop();
            isPreviewScheduled = false;
            if (pendingPreview is { } preview)
            {
                contentOperations?.Cancel(preview);
                pendingPreview = null;
            }
            if (hadPreview)
            {
                PreviewSummary = expressionSession.Localizer.GetString("Expression.Preview.Cancelled");
                StatusText = PreviewSummary;
                NotifyPreviewStateChanged();
            }
            return ValueTask.CompletedTask;
        }, _ => IsPreviewPending);
    }

    public IAppLocalizer Localizer => expressionSession.Localizer;

    public IExpressionAuthoringService? ExpressionAuthoringService { get; }

    public IReadOnlyList<ExpressionPresetViewModel> Presets { get; }

    public ExpressionPresetViewModel? SelectedPreset =>
        SelectedPresetIndex >= 0 && SelectedPresetIndex < Presets.Count ? Presets[SelectedPresetIndex] : null;

    public int SelectedPresetIndex
    {
        get => selectedPresetIndex;
        set
        {
            if (!SetProperty(ref selectedPresetIndex, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedPreset));
            if (SelectedPreset is { } preset)
            {
                settingExpressionFromPreset = true;
                try
                {
                    Expression = preset.ScriptText;
                }
                finally
                {
                    settingExpressionFromPreset = false;
                }
                ExpressionSourceName = preset.DisplayName;
                StatusText = expressionSession.Localizer.Format(
                    LocalizedMessage.Create("Status.LuaExpressionPresetSelected", ("preset", preset.DisplayName)));
                SchedulePreview();
            }
        }
    }

    public string Expression
    {
        get => expression;
        set
        {
            if (!SetProperty(ref expression, value ?? string.Empty))
            {
                return;
            }

            if (!settingExpressionFromPreset && SelectedPresetIndex >= 0)
            {
                selectedPresetIndex = -1;
                OnPropertyChanged(nameof(SelectedPresetIndex));
                OnPropertyChanged(nameof(SelectedPreset));
            }
            SchedulePreview();
        }
    }

    // Compatibility for callers that used the former toggle. Expression drafts are never projections.
    public bool ApplyExpression
    {
        get => false;
        set { }
    }

    public string ExpressionSourceName
    {
        get => expressionSourceName;
        set => SetProperty(ref expressionSourceName, value ?? string.Empty);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool CanBrowseScript => filePicker is not null;

    public bool IsPreviewPending => pendingPreview is not null || isPreviewScheduled;

    public bool CanPreparePreview => false;

    public bool CanApplyPreview => pendingPreview?.IsValid == true;

    public string PreviewSummary
    {
        get => previewSummary;
        private set => SetProperty(ref previewSummary, value);
    }

    public UiCommand BrowseScriptCommand { get; }

    public UiCommand ApplyCommand { get; }

    public UiCommand ConfirmApplyCommand { get; }

    public UiCommand CancelPreviewCommand { get; }

    public void RefreshPreviewNow()
    {
        previewTimer.Stop();
        isPreviewScheduled = false;
        if (contentOperations is null)
        {
            return;
        }

        if (pendingPreview is { } previous)
        {
            contentOperations.Cancel(previous);
        }

        isPreviewScheduled = false;
        pendingPreview = contentOperations.PrepareExpression(Expression);
        PreviewSummary = BuildPreviewSummary(pendingPreview, expressionSession.Localizer);
        var diagnostic = pendingPreview.IsValid
            ? null
            : expressionSession.ValidateLuaExpressionScript(Expression, logDiagnostics: true);
        StatusText = diagnostic is not null
            ? expressionSession.FormatDiagnosticForDisplay(diagnostic)
            : pendingPreview.IsValid
                ? PreviewSummary
                : string.Join(Environment.NewLine, pendingPreview.Errors);
        NotifyPreviewStateChanged();
    }

    public void Dispose()
    {
        previewTimer.Stop();
        previewTimer.Tick -= OnPreviewTimerTick;
        if (pendingPreview is { } preview)
        {
            contentOperations?.Cancel(preview);
            pendingPreview = null;
        }
    }

    private async ValueTask ApplyPreviewAsync(object? sender, CancellationToken cancellationToken)
    {
        if (contentOperations is null)
        {
            return;
        }

        // Apply always uses a candidate prepared from the latest draft, even if the debounce is pending.
        RefreshPreviewNow();
        if (pendingPreview is not { IsValid: true } preview)
        {
            return;
        }

        pendingPreview = null;
        PreviewSummary = string.Empty;
        NotifyPreviewStateChanged();
        var result = await contentOperations.ApplyAsync(preview, cancellationToken);
        if (result.Kind == TransactionOutcomeKind.Conflict)
        {
            RefreshPreviewNow();
            return;
        }

        if (result.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
        {
            expressionSession.ApplyLuaExpressionSettings(
                Expression,
                applyExpression: false,
                SelectedPreset?.Id ?? string.Empty,
                ExpressionSourceName);
            StatusText = expressionSession.Localizer.GetString("Status.Updated");
        }
        else
        {
            StatusText = string.Join(Environment.NewLine, result.Errors);
        }
        NotifyPreviewStateChanged();
    }

    private void SchedulePreview()
    {
        previewTimer.Stop();
        if (contentOperations is null)
        {
            return;
        }

        if (pendingPreview is { } previous)
        {
            contentOperations.Cancel(previous);
            pendingPreview = null;
        }
        PreviewSummary = Localizer.GetString("Expression.Preview.Pending");
        StatusText = PreviewSummary;
        isPreviewScheduled = true;
        NotifyPreviewStateChanged();
        previewTimer.Start();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs args)
    {
        previewTimer.Stop();
        isPreviewScheduled = false;
        RefreshPreviewNow();
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(IsPreviewPending));
        OnPropertyChanged(nameof(CanPreparePreview));
        OnPropertyChanged(nameof(CanApplyPreview));
        ConfirmApplyCommand.RaiseCanExecuteChanged();
        CancelPreviewCommand.RaiseCanExecuteChanged();
    }

    private static string BuildPreviewSummary(ChapterContentPreview preview, IAppLocalizer localizer) =>
        preview.Differences.IsEmpty
            ? localizer.GetString("Expression.Preview.NoChanges")
            : string.Join(Environment.NewLine, preview.Differences.Take(8).Select(difference =>
                DescribeDifference(preview, difference)));

    private static string DescribeDifference(ChapterContentPreview preview, ChapterContentDifference difference)
    {
        if (difference.ChapterId is not { } chapterId)
        {
            return $"{difference.Field}: {difference.Before} → {difference.After}";
        }

        var chapter = preview.Before.Tracks.SelectMany(static track => track.Chapters)
            .FirstOrDefault(item => item.Id == chapterId)
            ?? preview.Candidate.Tracks.SelectMany(static track => track.Chapters)
                .FirstOrDefault(item => item.Id == chapterId);
        var label = chapter is null ? chapterId.ToString() : $"#{chapter.DisplayNumber} {chapter.Name}";
        return $"{label} · {difference.Field}: {difference.Before} → {difference.After}";
    }

    private async ValueTask BrowseScriptAsync(CancellationToken cancellationToken)
    {
        if (filePicker is null)
        {
            return;
        }

        var path = await filePicker.PickLuaExpressionScriptAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Expression = await File.ReadAllTextAsync(path, cancellationToken);
            ExpressionSourceName = Path.GetFileName(path);
            SelectedPresetIndex = -1;
            var diagnostic = expressionSession.ValidateLuaExpressionScript(Expression, logDiagnostics: true);
            StatusText = diagnostic is null
                ? expressionSession.Localizer.Format(LocalizedMessage.Create(
                    "Status.LuaExpressionScriptLoaded", ("path", ExpressionSourceName)))
                : expressionSession.FormatDiagnosticForDisplay(diagnostic);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = expressionSession.Localizer.Format(
                LocalizedMessage.Create("Status.LuaExpressionScriptLoadFailed", ("path", Path.GetFileName(path))));
        }
    }
}

public sealed record ExpressionPresetViewModel(string Id, string DisplayName, string Description, string ScriptText);

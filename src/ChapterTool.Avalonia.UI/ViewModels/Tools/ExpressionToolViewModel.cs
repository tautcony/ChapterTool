using System.Globalization;
using Avalonia.Threading;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ExpressionToolViewModel : ObservableViewModel, IDisposable
{
    private readonly IExpressionSessionPort expressionSession;
    private readonly IChapterContentOperationPort? contentOperations;
    private readonly IFilePickerService? filePicker;
    private readonly Action? closeTool;
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private ChapterContentPreview? pendingPreview;
    private bool settingExpressionFromPreset;
    private int selectedPresetIndex = -1;
    private long draftRevision;
    private long preparedDraftRevision = -1;
    private string expression = string.Empty;
    private string expressionSourceName = string.Empty;
    private string statusText = string.Empty;
    private string previewSummary = string.Empty;
    private IReadOnlyList<ExpressionChapterRowViewModel> previewRows = [];
    private bool showAllChapters;
    private bool showFrames;

    public ExpressionToolViewModel(
        IExpressionSessionPort expressionSession,
        IFilePickerService? filePicker = null,
        IExpressionAuthoringService? expressionAuthoringService = null,
        Func<Exception, ValueTask>? errorHandler = null,
        IChapterContentOperationPort? contentOperations = null,
        Action? closeTool = null)
    {
        this.expressionSession = expressionSession;
        this.contentOperations = contentOperations;
        this.filePicker = filePicker;
        this.closeTool = closeTool;
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

        // Retain this command for callers that request an immediate preview.
        ApplyCommand = new UiCommand((_, _) =>
        {
            RefreshPreviewNow();
            return ValueTask.CompletedTask;
        }, _ => contentOperations is not null && !IsApplying)
        {
            ErrorHandler = errorHandler
        };
        ConfirmApplyCommand = new UiCommand(ApplyPreviewAsync, _ => CanApplyPreview)
        {
            ErrorHandler = errorHandler
        };
        CancelPreviewCommand = new UiCommand((_, _) =>
        {
            previewTimer.Stop();
            if (pendingPreview is { } preview)
            {
                contentOperations?.Cancel(preview);
                pendingPreview = null;
            }
            PreviewReadiness = ExpressionPreviewReadiness.Empty;
            Projection = null;
            previewRows = [];
            PropertyRows = [];
            PreviewSummary = Localizer.GetString("Expression.State.Empty");
            StatusText = PreviewSummary;
            NotifyPreviewStateChanged();
            closeTool?.Invoke();
            return ValueTask.CompletedTask;
        }, _ => !IsApplying);
        UpdatePreviewCommand = new UiCommand((_, _) =>
        {
            if (PreviewReadiness == ExpressionPreviewReadiness.Stale)
            {
                RefreshPreviewNow();
            }
            return ValueTask.CompletedTask;
        }, _ => PreviewReadiness == ExpressionPreviewReadiness.Stale && !IsApplying);

        if (contentOperations?.CanPrepareExpression == true && !string.IsNullOrWhiteSpace(Expression))
        {
            RefreshPreviewNow();
        }
        else if (contentOperations?.CanPrepareExpression == false)
        {
            PreviewReadiness = ExpressionPreviewReadiness.Unavailable;
            PreviewSummary = Localizer.GetString("Expression.State.Unavailable");
            StatusText = PreviewSummary;
        }
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

    public bool IsPreviewPending => PreviewReadiness is ExpressionPreviewReadiness.Waiting or ExpressionPreviewReadiness.Computing;

    public bool CanPreparePreview => false;

    public bool CanApplyPreview => PreviewReadiness == ExpressionPreviewReadiness.Ready
        && pendingPreview is { IsValid: true }
        && preparedDraftRevision == draftRevision
        && Projection?.HasChanges == true
        && !IsApplying;

    public ExpressionPreviewReadiness PreviewReadiness { get; private set; } = ExpressionPreviewReadiness.Unavailable;

    public bool IsApplying { get; private set; }

    public bool CanEditExpression => !IsApplying;

    public bool HasPreviewResult => Projection is not null;

    public bool HasPropertyChanges => PropertyRows.Count > 0;

    public bool TimesUnchanged => Projection?.TimesUnchanged == true;

    public bool ShowPreviewState => PreviewReadiness != ExpressionPreviewReadiness.Ready;

    public bool CanRefreshPreview => PreviewReadiness == ExpressionPreviewReadiness.Stale && !IsApplying;

    public bool HasExcludedSeparators => Projection?.ExcludedSeparatorCount > 0;

    public string ExcludedSeparatorSummary => Projection is null || Projection.ExcludedSeparatorCount == 0
        ? string.Empty
        : Localizer.Format(LocalizedMessage.Create("Expression.Preview.Separators", ("count", Projection.ExcludedSeparatorCount)));

    public string PreviewStateText => PreviewReadiness switch
    {
        ExpressionPreviewReadiness.Empty => Localizer.GetString("Expression.State.Empty"),
        ExpressionPreviewReadiness.Waiting => Localizer.GetString("Expression.State.Waiting"),
        ExpressionPreviewReadiness.Computing => Localizer.GetString("Expression.State.Computing"),
        ExpressionPreviewReadiness.Ready => Localizer.GetString("Expression.State.Ready"),
        ExpressionPreviewReadiness.Unchanged => Localizer.GetString("Expression.State.Unchanged"),
        ExpressionPreviewReadiness.Invalid => StatusText,
        ExpressionPreviewReadiness.Stale => Localizer.GetString("Expression.State.Stale"),
        ExpressionPreviewReadiness.Applying => Localizer.GetString("Expression.State.Applying"),
        ExpressionPreviewReadiness.Failed => StatusText,
        _ => Localizer.GetString("Expression.State.Unavailable")
    };

    public string FooterStatusText => PreviewReadiness switch
    {
        ExpressionPreviewReadiness.Applying => Localizer.GetString("Expression.State.Applying"),
        ExpressionPreviewReadiness.Failed => StatusText,
        _ => string.Empty
    };

    public string ScopeSummary => Projection is null
        ? string.Empty
        : Localizer.Format(LocalizedMessage.Create(
            "Expression.Preview.Scope",
            ("track", Projection.ScopeTrackName),
            ("count", Projection.ParticipatingChapterCount)));

    public string TimeChangeSummary => Projection is null ? string.Empty : Localizer.Format(
        LocalizedMessage.Create("Expression.Review.TimeSummary",
            ("times", Projection.TimeChangedChapterCount), ("count", Projection.ParticipatingChapterCount)));

    public string OtherChangeSummary => Projection is null ? string.Empty : Localizer.Format(
        LocalizedMessage.Create("Expression.Review.OtherSummary",
            ("affected", Projection.AffectedChapterCount), ("frames", Projection.FrameInformationChangedChapterCount), ("properties", Projection.Properties.Length)));

    public ExpressionPreviewProjection? Projection { get; private set; }

    public IReadOnlyList<ExpressionChapterRowViewModel> PreviewRows => showAllChapters
        ? previewRows
        : [.. previewRows.Where(static row => row.Changes != ExpressionChapterChangeKind.None)];

    public IReadOnlyList<ExpressionPropertyRowViewModel> PropertyRows { get; private set; } = [];

    public bool ShowAllChapters
    {
        get => showAllChapters;
        set
        {
            if (SetProperty(ref showAllChapters, value))
            {
                OnPropertyChanged(nameof(PreviewRows));
            }
        }
    }

    public bool ShowFrames
    {
        get => showFrames;
        set
        {
            if (SetProperty(ref showFrames, value))
            {
                previewRows = BuildRows(Projection, value, Localizer);
                OnPropertyChanged(nameof(PreviewRows));
            }
        }
    }

    public string PreviewSummary
    {
        get => previewSummary;
        private set => SetProperty(ref previewSummary, value);
    }

    public UiCommand BrowseScriptCommand { get; }

    public UiCommand ApplyCommand { get; }

    public UiCommand ConfirmApplyCommand { get; }

    public UiCommand CancelPreviewCommand { get; }

    public UiCommand UpdatePreviewCommand { get; }

    public void RefreshPreviewNow()
    {
        previewTimer.Stop();
        if (contentOperations is null || !contentOperations.CanPrepareExpression)
        {
            PreviewReadiness = ExpressionPreviewReadiness.Unavailable;
            PreviewSummary = Localizer.GetString("Expression.State.Unavailable");
            StatusText = PreviewSummary;
            NotifyPreviewStateChanged();
            return;
        }

        if (pendingPreview is { } previous)
        {
            contentOperations.Cancel(previous);
            pendingPreview = null;
        }

        if (string.IsNullOrWhiteSpace(Expression))
        {
            Projection = null;
            previewRows = [];
            PropertyRows = [];
            PreviewReadiness = ExpressionPreviewReadiness.Empty;
            PreviewSummary = Localizer.GetString("Expression.State.Empty");
            StatusText = PreviewSummary;
            NotifyPreviewStateChanged();
            return;
        }

        PreviewReadiness = ExpressionPreviewReadiness.Computing;
        PreviewSummary = Localizer.GetString("Expression.Preview.Pending");
        StatusText = PreviewSummary;
        NotifyPreviewStateChanged();
        pendingPreview = contentOperations.PrepareExpression(Expression);
        Projection = ExpressionPreviewProjector.Build(pendingPreview);
        preparedDraftRevision = draftRevision;
        previewRows = BuildRows(Projection, showFrames, Localizer);
        PropertyRows = [.. Projection.Properties.Select(row => new ExpressionPropertyRowViewModel(row, Localizer))];
        PreviewSummary = BuildPreviewSummary(Projection, expressionSession.Localizer);
        var diagnostic = pendingPreview.IsValid
            ? null
            : expressionSession.ValidateLuaExpressionScript(Expression, logDiagnostics: true);
        PreviewReadiness = !pendingPreview.IsValid
            ? ExpressionPreviewReadiness.Invalid
            : Projection.ParticipatingChapterCount == 0
            ? ExpressionPreviewReadiness.Unavailable
            : Projection.HasChanges
                ? ExpressionPreviewReadiness.Ready
                : ExpressionPreviewReadiness.Unchanged;
        StatusText = diagnostic is not null
            ? expressionSession.FormatDiagnosticForDisplay(diagnostic)
            : pendingPreview.IsValid ? PreviewSummary : string.Join(Environment.NewLine, pendingPreview.Errors);
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
        if (!CanApplyPreview || contentOperations is null || pendingPreview is not { IsValid: true } preview)
        {
            return;
        }

        IsApplying = true;
        PreviewReadiness = ExpressionPreviewReadiness.Applying;
        StatusText = Localizer.GetString("Expression.State.Applying");
        NotifyPreviewStateChanged();
        try
        {
            var result = await contentOperations.ApplyAsync(preview, cancellationToken);
            if (result.Kind == TransactionOutcomeKind.Conflict)
            {
                PreviewReadiness = ExpressionPreviewReadiness.Stale;
                StatusText = Localizer.GetString("Expression.State.Stale");
                return;
            }

            if (result.Kind == TransactionOutcomeKind.Committed && Projection is { } appliedProjection)
            {
                expressionSession.SaveAppliedExpressionPreview(
                    Expression,
                    SelectedPreset?.Id ?? string.Empty,
                    ExpressionSourceName,
                    appliedProjection);
                pendingPreview = null;
                StatusText = Localizer.Format(LocalizedMessage.Create(
                    "Expression.Preview.Applied",
                    ("affected", Projection?.AffectedChapterCount ?? 0),
                    ("times", Projection?.TimeChangedChapterCount ?? 0),
                    ("frames", Projection?.FrameInformationChangedChapterCount ?? 0)));
                closeTool?.Invoke();
            }
            else
            {
                PreviewReadiness = ExpressionPreviewReadiness.Failed;
                StatusText = string.Join(Environment.NewLine, result.Errors);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException)
        {
            PreviewReadiness = ExpressionPreviewReadiness.Failed;
            StatusText = exception.Message;
        }
        finally
        {
            IsApplying = false;
            if (PreviewReadiness == ExpressionPreviewReadiness.Applying)
            {
                PreviewReadiness = ExpressionPreviewReadiness.Ready;
            }
            NotifyPreviewStateChanged();
        }
    }

    private void SchedulePreview()
    {
        previewTimer.Stop();
        draftRevision++;
        preparedDraftRevision = -1;
        if (contentOperations is null || !contentOperations.CanPrepareExpression)
        {
            PreviewReadiness = ExpressionPreviewReadiness.Unavailable;
            PreviewSummary = Localizer.GetString("Expression.State.Unavailable");
            StatusText = PreviewSummary;
            NotifyPreviewStateChanged();
            return;
        }

        if (pendingPreview is { } previous)
        {
            contentOperations.Cancel(previous);
            pendingPreview = null;
        }
        Projection = null;
        previewRows = [];
        PropertyRows = [];
        if (string.IsNullOrWhiteSpace(Expression))
        {
            PreviewReadiness = ExpressionPreviewReadiness.Empty;
            PreviewSummary = Localizer.GetString("Expression.State.Empty");
            StatusText = PreviewSummary;
            NotifyPreviewStateChanged();
            return;
        }

        PreviewReadiness = ExpressionPreviewReadiness.Waiting;
        PreviewSummary = Localizer.GetString("Expression.Preview.Pending");
        StatusText = PreviewSummary;
        NotifyPreviewStateChanged();
        previewTimer.Start();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs args)
    {
        previewTimer.Stop();
        RefreshPreviewNow();
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(IsPreviewPending));
        OnPropertyChanged(nameof(IsApplying));
        OnPropertyChanged(nameof(CanEditExpression));
        OnPropertyChanged(nameof(HasPreviewResult));
        OnPropertyChanged(nameof(HasPropertyChanges));
        OnPropertyChanged(nameof(TimesUnchanged));
        OnPropertyChanged(nameof(CanRefreshPreview));
        OnPropertyChanged(nameof(HasExcludedSeparators));
        OnPropertyChanged(nameof(ExcludedSeparatorSummary));
        OnPropertyChanged(nameof(PreviewStateText));
        OnPropertyChanged(nameof(ShowPreviewState));
        OnPropertyChanged(nameof(FooterStatusText));
        OnPropertyChanged(nameof(ScopeSummary));
        OnPropertyChanged(nameof(TimeChangeSummary));
        OnPropertyChanged(nameof(OtherChangeSummary));
        OnPropertyChanged(nameof(CanPreparePreview));
        OnPropertyChanged(nameof(CanApplyPreview));
        OnPropertyChanged(nameof(PreviewReadiness));
        OnPropertyChanged(nameof(Projection));
        OnPropertyChanged(nameof(PreviewRows));
        OnPropertyChanged(nameof(PropertyRows));
        ConfirmApplyCommand.RaiseCanExecuteChanged();
        CancelPreviewCommand.RaiseCanExecuteChanged();
        UpdatePreviewCommand.RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<ExpressionChapterRowViewModel> BuildRows(
        ExpressionPreviewProjection? preview,
        bool showFrames,
        IAppLocalizer localizer) => preview?.Chapters
            .Select(row => new ExpressionChapterRowViewModel(row, showFrames, localizer))
            .ToArray() ?? [];

    private static string BuildPreviewSummary(ExpressionPreviewProjection preview, IAppLocalizer localizer) =>
        localizer.Format(LocalizedMessage.Create(
            "Expression.Preview.Summary",
            ("affected", preview.AffectedChapterCount),
            ("times", preview.TimeChangedChapterCount),
            ("frames", preview.FrameInformationChangedChapterCount),
            ("properties", preview.Properties.Length)));

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

public enum ExpressionPreviewReadiness
{
    Unavailable,
    Empty,
    Waiting,
    Computing,
    Ready,
    Unchanged,
    Invalid,
    Stale,
    Applying,
    Failed
}

public sealed record ExpressionChapterRowViewModel
{
    public ExpressionChapterRowViewModel(ExpressionChapterComparison comparison, bool showFrames, IAppLocalizer localizer)
    {
        Comparison = comparison;
        var before = comparison.Before;
        var candidate = comparison.Candidate;
        NumberAndName = candidate is null && before is null
            ? localizer.GetString("Expression.Preview.Chapter")
            : $"{(candidate ?? before)!.DisplayNumber} · {(candidate ?? before)!.Name}";
        BeforeValue = FormatValue(before, comparison.BeforeFrames, showFrames, localizer);
        CandidateValue = FormatValue(candidate, comparison.CandidateFrames, showFrames, localizer);
        DeltaValue = FormatDelta(comparison, showFrames, localizer);
        BeforeFrameDetails = FormatFrameDetails(comparison.BeforeFrames, localizer);
        CandidateFrameDetails = FormatFrameDetails(comparison.CandidateFrames, localizer);
        BeforeAccuracy = FormatAccuracy(comparison.BeforeFrames?.Accuracy, localizer);
        CandidateAccuracy = FormatAccuracy(comparison.CandidateFrames?.Accuracy, localizer);
    }

    public ExpressionChapterComparison Comparison { get; }

    public ChapterId Id => Comparison.Id;

    public ExpressionChapterChangeKind Changes => Comparison.Changes;

    public string NumberAndName { get; }

    public string BeforeValue { get; }

    public string CandidateValue { get; }

    public string DeltaValue { get; }

    public string BeforeFrameDetails { get; }

    public string CandidateFrameDetails { get; }

    public string BeforeAccuracy { get; }

    public string CandidateAccuracy { get; }

    private static string FormatValue(EditableChapter? chapter, ExpressionFrameValue? frames, bool showFrames, IAppLocalizer localizer) =>
        chapter is null
            ? localizer.GetString("Expression.Value.Missing")
            : showFrames ? FormatFrameDetails(frames, localizer) : FormatTicks(chapter.StartTicks);

    private static string FormatDelta(ExpressionChapterComparison comparison, bool showFrames, IAppLocalizer localizer)
    {
        if (showFrames)
        {
            return comparison.DeltaFrames is { } frames
                ? localizer.Format(LocalizedMessage.Create("Expression.Preview.DeltaFrames", ("value", frames)))
                : localizer.GetString("Expression.Value.IncompatibleFrames");
        }

        if (comparison.DeltaTicks is not { } ticks)
        {
            return localizer.GetString("Expression.Value.Missing");
        }

        var sign = ticks > 0 ? "+" : ticks < 0 ? "−" : string.Empty;
        return sign + FormatTicks(Math.Abs(ticks));
    }

    private static string FormatFrameDetails(ExpressionFrameValue? value, IAppLocalizer localizer)
    {
        if (value is null || value.IsMissing)
        {
            return localizer.GetString("Expression.Value.NotCalculated");
        }

        var rate = value.FrameRate is { } fps
            ? $"{fps.Numerator}/{fps.Denominator} {localizer.GetString("Expression.Unit.FramesPerSecond")}"
            : localizer.GetString("Expression.Value.NotSet");
        return $"{value.Text} {localizer.GetString("Expression.Unit.FramesShort")} · {rate}";
    }

    private static string FormatAccuracy(FrameAccuracy? accuracy, IAppLocalizer localizer) => accuracy switch
    {
        FrameAccuracy.Accurate => localizer.GetString("Expression.FrameAccuracy.Accurate"),
        FrameAccuracy.Inexact => localizer.GetString("Expression.FrameAccuracy.Inexact"),
        _ => localizer.GetString("Expression.FrameAccuracy.NotCalculated")
    };

    internal static string FormatTicks(long ticks)
    {
        var totalMilliseconds = (long)decimal.Round(
            ticks / (decimal)TimeSpan.TicksPerMillisecond,
            0,
            MidpointRounding.ToEven);
        var hours = totalMilliseconds / 3_600_000;
        var minutes = totalMilliseconds / 60_000 % 60;
        var seconds = totalMilliseconds / 1_000 % 60;
        var milliseconds = totalMilliseconds % 1_000;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{minutes:00}:{seconds:00}.{milliseconds:000}");
    }
}

public sealed record ExpressionPropertyRowViewModel
{
    public ExpressionPropertyRowViewModel(ExpressionPropertyComparison comparison, IAppLocalizer localizer)
    {
        OwnerLabel = comparison.OwnerKind switch
        {
            ExpressionPropertyOwnerKind.Document => localizer.GetString("Expression.PropertyOwner.Document"),
            ExpressionPropertyOwnerKind.Track => localizer.GetString("Expression.PropertyOwner.Track"),
            ExpressionPropertyOwnerKind.Segment => localizer.GetString("Expression.PropertyOwner.Segment"),
            _ => localizer.GetString("Expression.PropertyOwner.Chapter")
        };
        OwnerName = comparison.OwnerName;
        PropertyLabel = comparison.Property switch
        {
            "Title" => localizer.GetString("Expression.Property.Title"),
            "SourceName" => localizer.GetString("Expression.Property.Source"),
            "ImportFormat" => localizer.GetString("Expression.Property.ImportFormat"),
            "Duration" => localizer.GetString("Expression.Property.Duration"),
            "FrameRate" => localizer.GetString("Expression.Property.FrameRate"),
            "Name" => localizer.GetString("Expression.Property.Name"),
            "DisplayNumber" => localizer.GetString("Expression.Property.Number"),
            "Kind" => localizer.GetString("Expression.Property.Type"),
            "Presence" => localizer.GetString("Expression.Property.Presence"),
            "TrackOrder" => localizer.GetString("Expression.Property.TrackOrder"),
            _ => localizer.GetString("Expression.Property.Additional")
        };
        BeforeValue = FormatValue(comparison.Property, comparison.Before, localizer);
        CandidateValue = FormatValue(comparison.Property, comparison.Candidate, localizer);
    }

    public string OwnerLabel { get; }

    public string OwnerName { get; }

    public string PropertyLabel { get; }

    public string BeforeValue { get; }

    public string CandidateValue { get; }

    private static string FormatValue(string property, object? value, IAppLocalizer localizer)
    {
        if (value is null)
        {
            return localizer.GetString("Expression.Value.NotSet");
        }

        if (value is ChapterFrameRate rate)
        {
            return $"{rate.Numerator}/{rate.Denominator} {localizer.GetString("Expression.Unit.FramesPerSecond")}";
        }
        if (value is ChapterDuration duration)
        {
            if (!duration.IsKnown)
            {
                return localizer.GetString("Expression.Value.NotSet");
            }
            return ExpressionChapterRowViewModel.FormatTicks(duration.Ticks);
        }

        if (value is string text && property is "Title" or "SourceName" or "Name")
        {
            return text;
        }

        if (value is int or long or bool)
        {
            return Convert.ToString(value, CultureInfo.CurrentUICulture) ?? string.Empty;
        }
        return localizer.GetString("Expression.Property.ValueChanged");
    }
}

using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ExpressionToolViewModel : ObservableViewModel
{
    private readonly IExpressionSessionPort expressionSession;
    private readonly IChapterContentOperationPort? contentOperations;
    private readonly IFilePickerService? filePicker;
    private ChapterContentPreview? pendingPreview;

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
        Expression = expressionSession.Expression;
        ApplyExpression = expressionSession.ApplyExpression;
        ExpressionSourceName = expressionSession.ExpressionSourceName;
        Presets =
        [
            .. expressionSession.ExpressionPresets
                .Select(static preset =>
                    new ExpressionPresetViewModel(preset.Id, preset.DisplayName, preset.Description, preset.ScriptText))
        ];
        SelectedPresetIndex = Presets.ToList().FindIndex(preset => string.Equals(preset.Id, expressionSession.ExpressionPresetId, StringComparison.Ordinal));
        BrowseScriptCommand = new UiCommand(async (_, token) => await BrowseScriptAsync(token), _ => this.filePicker is not null)
        {
            ErrorHandler = errorHandler
        };
        ApplyCommand = new UiCommand((parameter, _) =>
        {
            if (parameter is ExpressionToolViewModel viewModel)
            {
                if (contentOperations is not null)
                {
                    viewModel.pendingPreview = contentOperations!.PrepareExpression(viewModel.Expression);
                    viewModel.OnPropertyChanged(nameof(IsPreviewPending));
                    viewModel.OnPropertyChanged(nameof(CanApplyPreview));
                    viewModel.PreviewSummary = BuildPreviewSummary(viewModel.pendingPreview);
                    viewModel.StatusText = viewModel.pendingPreview.IsValid
                        ? viewModel.PreviewSummary
                        : string.Join("; ", viewModel.pendingPreview.Errors);
                    viewModel.ConfirmApplyCommand!.RaiseCanExecuteChanged();
                    viewModel.CancelPreviewCommand!.RaiseCanExecuteChanged();
                    return ValueTask.CompletedTask;
                }

                var diagnostic = expressionSession.ApplyLuaExpressionSettings(
                    viewModel.Expression,
                    viewModel.ApplyExpression,
                    viewModel.SelectedPreset?.Id ?? string.Empty,
                    viewModel.ExpressionSourceName);
                viewModel.StatusText = diagnostic is null
                    ? expressionSession.Localizer.GetString("Status.Updated")
                    : expressionSession.FormatDiagnosticForDisplay(diagnostic);
            }

            return ValueTask.CompletedTask;
        })
        {
            ErrorHandler = errorHandler
        };
        ConfirmApplyCommand = new UiCommand(async (_, token) =>
        {
            if (contentOperations is null || pendingPreview is null || !pendingPreview.IsValid)
            {
                return;
            }

            var preview = pendingPreview;
            pendingPreview = null;
            OnPropertyChanged(nameof(IsPreviewPending));
            OnPropertyChanged(nameof(CanApplyPreview));
            var result = await contentOperations!.ApplyAsync(preview, token);
            if (result.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
            {
                expressionSession.ApplyLuaExpressionSettings(
                    Expression,
                    applyExpression: false,
                    SelectedPreset?.Id ?? string.Empty,
                    ExpressionSourceName);
                ApplyExpression = false;
            }
            StatusText = result.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange
                ? expressionSession.Localizer.GetString("Status.Updated")
                : string.Join("; ", result.Errors);
            ConfirmApplyCommand!.RaiseCanExecuteChanged();
            CancelPreviewCommand!.RaiseCanExecuteChanged();
        })
        {
            ErrorHandler = errorHandler
        };
        CancelPreviewCommand = new UiCommand((_, _) =>
        {
            if (pendingPreview is { } preview)
            {
                contentOperations!.Cancel(preview);
                pendingPreview = null;
                PreviewSummary = string.Empty;
                StatusText = expressionSession.Localizer.GetString("Status.Updated");
                OnPropertyChanged(nameof(IsPreviewPending));
                OnPropertyChanged(nameof(CanApplyPreview));
            }

            ConfirmApplyCommand!.RaiseCanExecuteChanged();
            CancelPreviewCommand!.RaiseCanExecuteChanged();
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
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedPreset));
            if (SelectedPreset is { } preset)
            {
                Expression = preset.ScriptText;
                ExpressionSourceName = preset.DisplayName;
                StatusText = expressionSession.Localizer.Format(LocalizedMessage.Create("Status.LuaExpressionPresetSelected", ("preset", preset.DisplayName)));
            }
        }
    }

    public string Expression
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool ApplyExpression
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ExpressionSourceName
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool CanBrowseScript => filePicker is not null;

    public bool IsPreviewPending => pendingPreview is not null;

    public bool CanPreparePreview => pendingPreview is null;

    public bool CanApplyPreview => pendingPreview?.IsValid == true;

    public string PreviewSummary
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public UiCommand BrowseScriptCommand { get; }

    public UiCommand ApplyCommand { get; }

    public UiCommand ConfirmApplyCommand { get; }

    public UiCommand CancelPreviewCommand { get; }

    private static string BuildPreviewSummary(ChapterContentPreview? preview) => preview is null || preview.Differences.IsEmpty
        ? "No chapter values will change."
        : string.Join("; ", preview.Differences.Take(4).Select(static difference =>
            $"{difference.Field}: '{difference.Before}' → '{difference.After}'"));

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
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            Expression = text;
            ExpressionSourceName = Path.GetFileName(path);
            SelectedPresetIndex = -1;
            var diagnostic = expressionSession.ValidateLuaExpressionScript(Expression, logDiagnostics: true);
            StatusText = diagnostic is null
                ? expressionSession.Localizer.Format(LocalizedMessage.Create("Status.LuaExpressionScriptLoaded", ("path", ExpressionSourceName)))
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

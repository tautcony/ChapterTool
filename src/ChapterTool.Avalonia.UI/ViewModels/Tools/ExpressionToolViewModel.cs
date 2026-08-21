using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ExpressionToolViewModel : ObservableViewModel
{
    private readonly IExpressionSessionPort expressionSession;
    private readonly IFilePickerService? filePicker;

    public ExpressionToolViewModel(
        IExpressionSessionPort expressionSession,
        IFilePickerService? filePicker = null,
        IExpressionAuthoringService? expressionAuthoringService = null,
        Func<Exception, ValueTask>? errorHandler = null)
    {
        this.expressionSession = expressionSession;
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

    public UiCommand BrowseScriptCommand { get; }

    public UiCommand ApplyCommand { get; }

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

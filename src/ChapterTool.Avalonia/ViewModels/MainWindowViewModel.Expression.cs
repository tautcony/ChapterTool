using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Session;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async ValueTask<ChapterDiagnostic?> LoadLuaExpressionScriptAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var diagnostic = ApplyExpressionState(
            expression: string.IsNullOrWhiteSpace(text) ? "t" : text,
            applyExpression: true,
            expressionPresetId: string.Empty,
            expressionSourceName: Path.GetFileName(path),
            logDiagnostics: true);

        if (diagnostic is null)
        {
            SetStatus("Status.LuaExpressionScriptLoaded", ("path", ExpressionSourceName));
            LogStatus();
        }

        return diagnostic;
    }

    public ChapterDiagnostic? ApplyLuaExpressionSettings(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName) =>
        ApplyExpressionState(expression, applyExpression, expressionPresetId, expressionSourceName, logDiagnostics: true);

    public ChapterDiagnostic? ValidateLuaExpressionScript(string scriptText, bool logDiagnostics)
    {
        var result = expressionEngine.Evaluate(
            string.IsNullOrWhiteSpace(scriptText) ? "t" : scriptText,
            ChapterExpressionValidation.CreateContext(currentInfo));
        if (logDiagnostics)
        {
            LogDiagnostics(Localizer.GetString("Operation.LuaExpressionScript"), result.Diagnostics);
        }

        return result.Diagnostics.FirstOrDefault();
    }

    private ChapterDiagnostic? ApplyExpressionState(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName,
        bool logDiagnostics)
    {
        isMutatingExpressionState = true;
        try
        {
            workspace.ApplyExpressionFields(expression, applyExpression, expressionPresetId, expressionSourceName);
            OnPropertyChanged(nameof(Expression));
            OnPropertyChanged(nameof(ApplyExpression));
            OnPropertyChanged(nameof(ExpressionPresetId));
            OnPropertyChanged(nameof(ExpressionSourceName));
        }
        finally
        {
            isMutatingExpressionState = false;
        }

        RefreshRows();

        if (!ApplyExpression)
        {
            SetStatus("Status.Updated");
            LogStatus();
            NotifyStateChanged();
            return null;
        }

        var diagnostic = ValidateLuaExpressionScript(Expression, logDiagnostics);
        if (diagnostic is not null)
        {
            SetStatus(null, diagnostic);
            LogStatus(LogLevelFor(diagnostic.Severity));
        }
        else
        {
            SetStatus("Status.Updated");
            LogStatus();
        }

        NotifyStateChanged();
        return diagnostic;
    }

    public string FormatDiagnosticForDisplay(ChapterDiagnostic diagnostic) => LocalizeDiagnostic(diagnostic);

    private void RefreshRows()
    {
        if (currentInfo is null)
        {
            workspace.ClearProjectionCache();
            Rows.Clear();
            return;
        }

        ApplyProjectionToRows(CurrentOutputProjection());
    }

    private void ApplyProjectionToRows(ChapterOutputProjectionResult projection)
    {
        ReportProjectionExpressionDiagnostics(projection.Diagnostics);

        // Keep the last successful projection on screen while the expression is mid-edit invalid.
        // Failed evaluations would otherwise snap back to source times before the user finishes typing.
        if (ApplyExpression
            && projection.Diagnostics.Any(ChapterExpressionValidation.IsLuaExpressionDiagnostic)
            && workspace.LastSuccessfulExpressionProjection is not null)
        {
            return;
        }

        if (!ApplyExpression || !projection.Diagnostics.Any(ChapterExpressionValidation.IsLuaExpressionDiagnostic))
        {
            workspace.LastSuccessfulExpressionProjection = ApplyExpression ? projection : null;
        }

        Rows.Clear();
        foreach (var chapter in projection.OutputChapters)
        {
            Rows.Add(new ChapterRowViewModel(chapter, formatter));
        }
    }

    private void ReportProjectionExpressionDiagnostics(IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        if (!ApplyExpression)
        {
            lastExpressionDiagnosticSignature = null;
            return;
        }

        var diagnostic = diagnostics.FirstOrDefault(ChapterExpressionValidation.IsLuaExpressionDiagnostic);
        if (diagnostic is null)
        {
            lastExpressionDiagnosticSignature = null;
            return;
        }

        SetStatus(null, diagnostic);
        var signature = $"{Expression}\n{diagnostic.Code}\n{diagnostic.Message}";
        if (string.Equals(signature, lastExpressionDiagnosticSignature, StringComparison.Ordinal))
        {
            return;
        }

        lastExpressionDiagnosticSignature = signature;
        LogDiagnostics(Localizer.GetString("Operation.LuaExpressionScript"), [diagnostic]);
        LogStatus(LogLevelFor(diagnostic.Severity));
    }

    private ChapterOutputProjectionResult CurrentOutputProjection() =>
        currentInfo is null
            ? new ChapterOutputProjectionResult(
                new ChapterSet(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []),
                [])
            : outputProjectionService.Project(currentInfo, CurrentExportOptions());

    private ChapterExportOptions CurrentExportOptionsForProjectedInfo() =>
        workspace.CreateExportOptionsForProjectedInfo();
}

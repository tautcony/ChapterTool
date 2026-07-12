using System.Collections.ObjectModel;
using ChapterTool.Avalonia.Session;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.Workflows;

/// <summary>
/// Shares workspace-backed projection, export options, and row materialization across preview and save paths.
/// </summary>
internal sealed class ProjectionFacade(
    ChapterWorkspace workspace,
    IChapterExpressionEngine expressionEngine,
    IChapterTimeFormatter formatter)
{
    private readonly ChapterOutputProjectionService outputProjectionService = new(expressionEngine);

    public ChapterExportOptions CreateExportOptions() => workspace.CreateExportOptions();

    public ChapterExportOptions CreateExportOptionsForProjectedInfo() => workspace.CreateExportOptionsForProjectedInfo();

    public ChapterOutputProjectionResult ProjectCurrent() =>
        workspace.CurrentChapterSet is null
            ? new ChapterOutputProjectionResult(EmptyChapterSet(), [])
            : outputProjectionService.Project(workspace.CurrentChapterSet, CreateExportOptions());

    public ProjectionRefreshResult RefreshRows(ObservableCollection<ChapterRowViewModel> rows, bool applyExpression)
    {
        if (workspace.CurrentChapterSet is null)
        {
            workspace.ClearProjectionCache();
            rows.Clear();
            return new ProjectionRefreshResult(null, true);
        }

        var projection = ProjectCurrent();
        var hasExpressionDiagnostic = projection.Diagnostics.Any(ChapterExpressionValidation.IsLuaExpressionDiagnostic);
        if (applyExpression && hasExpressionDiagnostic && workspace.LastSuccessfulExpressionProjection is not null)
        {
            return new ProjectionRefreshResult(projection, false);
        }

        if (!applyExpression || !hasExpressionDiagnostic)
        {
            workspace.LastSuccessfulExpressionProjection = applyExpression ? projection : null;
        }

        rows.Clear();
        foreach (var chapter in projection.OutputChapters)
        {
            rows.Add(new ChapterRowViewModel(chapter, formatter));
        }

        return new ProjectionRefreshResult(projection, true);
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}

internal sealed record ProjectionRefreshResult(ChapterOutputProjectionResult? Projection, bool RowsUpdated);

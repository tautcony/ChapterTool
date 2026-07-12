using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshRows()
    {
        var refresh = projectionFacade.RefreshRows(Rows, ApplyExpression);
        if (refresh.Projection is not null)
        {
            ReportProjectionExpressionDiagnostics(refresh.Projection.Diagnostics);
        }
    }

    internal void RefreshRowsFromPort() => RefreshRows();

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
        projectionFacade.ProjectCurrent();

    private ChapterExportOptions CurrentExportOptionsForProjectedInfo() =>
        projectionFacade.CreateExportOptionsForProjectedInfo();
}

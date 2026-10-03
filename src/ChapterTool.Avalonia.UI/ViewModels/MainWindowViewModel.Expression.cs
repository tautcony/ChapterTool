using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains expression diagnostics and chapter row behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private void RefreshRows()
    {
        workspaceContentRows.RefreshRows(Rows);
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
        LogDiagnostics("Lua expression script", [diagnostic]);
    }

}

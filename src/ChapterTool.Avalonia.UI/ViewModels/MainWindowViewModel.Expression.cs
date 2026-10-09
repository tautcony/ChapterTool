using System.Globalization;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains expression diagnostics and chapter row behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private void RefreshRows()
    {
        var display = CurrentInfo is null ? null : ClipEditingCoordinator.UpdateFrames(CurrentInfo,
            selectedFrameRateOption, RoundFrames ? 0 : EditingOptions.FrameDisplay == ChapterTool.Core.Editing.FrameDisplayMode.DecimalPlaces ? EditingOptions.EffectiveFrameDecimalPlaces : -1,
            FrameAccuracyTolerance, configuredFrameRate).FrameResult;
        DisplayFrameRate = display?.FramesPerSecond ?? 0m;
        workspaceContentRows.RefreshRows(Rows, display?.Info);
        SetNarrowPreviewLayout(isNarrowPreviewLayout);
        UpdateInlinePreviewProjection();
    }

    private void UpdateInlinePreviewProjection()
    {
        var preview = pendingContentPreview;
        IReadOnlyDictionary<(ChapterTrackId TrackId, ChapterId ChapterId), ExpressionChapterComparison> comparisons = preview is { IsValid: true }
            && Workspace.IsContentTokenCurrent(preview.BaseToken)
            ? ExpressionPreviewProjector.Build(preview).Chapters.ToDictionary(item => (item.TrackId, item.Id))
            : new Dictionary<(ChapterTrackId TrackId, ChapterId ChapterId), ExpressionChapterComparison>();
        foreach (var row in Rows)
        {
            var comparison = row.TrackId is { } trackId && row.ChapterId is { } chapterId
                && comparisons.TryGetValue((trackId, chapterId), out var mappedComparison)
                    ? mappedComparison
                    : null;
            row.UpdatePreviewComparison(comparison, DisplayFrameRate, Localizer.GetString("Expression.Value.NotCalculated"));
            row.PreviewTimeText = row.HasPreviewTimeChange && row.PreviewComparison?.Candidate is { } candidate
                ? ExpressionChapterRowViewModel.FormatTicks(candidate.StartTicks, CultureInfo.CurrentUICulture)
                : string.Empty;
            row.PreviewBeforeTimeText = row.HasPreviewTimeChange && row.PreviewComparison?.Before is { } before
                ? ExpressionChapterRowViewModel.FormatTicks(before.StartTicks, CultureInfo.CurrentUICulture)
                : string.Empty;
            row.PreviewTimeDelta = row.PreviewComparison is { } timeComparison && row.HasPreviewTimeChange
                ? new ExpressionChapterRowViewModel(timeComparison, showFrames: false, Localizer).DeltaValue
                : string.Empty;
            row.PreviewName = row.HasPreviewNameChange
                ? row.PreviewComparison?.Candidate?.Name ?? Localizer.GetString("Expression.Value.Missing")
                : string.Empty;
            row.PreviewNumber = row.HasPreviewNumberChange
                ? row.PreviewComparison?.Candidate?.DisplayNumber.ToString() ?? Localizer.GetString("Expression.Value.Missing")
                : string.Empty;
            row.PreviewFrames = row.HasPreviewFramesChange
                ? row.PreviewComparison?.CandidateFrames?.Text ?? Localizer.GetString("Expression.Value.NotCalculated")
                : string.Empty;
            row.PreviewBeforeFrames = row.HasPreviewFramesChange
                ? row.PreviewFrameBaseline
                : string.Empty;
            row.PreviewTimeAccessibleName = row.HasPreviewTimeChange
                ? FormatPreviewAccessibleName(Localizer.GetString("Grid.Time"), row.PreviewBeforeTimeText, row.PreviewTimeText, row.PreviewTimeDelta)
                : string.Empty;
            row.PreviewNameAccessibleName = row.HasPreviewNameChange
                ? FormatPreviewAccessibleName(Localizer.GetString("Grid.Name"), row.Name, row.PreviewName)
                : string.Empty;
            row.PreviewNumberAccessibleName = row.HasPreviewNumberChange
                ? FormatPreviewAccessibleName(Localizer.GetString("Grid.Number"), row.Number.ToString(CultureInfo.CurrentCulture), row.PreviewNumber)
                : string.Empty;
            row.PreviewFramesAccessibleName = row.HasPreviewFramesChange
                ? FormatPreviewAccessibleName(Localizer.GetString("Grid.Frames"), row.PreviewBeforeFrames, row.PreviewFrames)
                : string.Empty;
        }
    }

    private string FormatPreviewAccessibleName(string field, string before, string candidate, string delta = "") =>
        Localizer.Format(LocalizedMessage.Create(
            "Expression.Preview.ComparisonAccessibleName",
            ("field", field),
            ("before", before),
            ("candidate", candidate),
            ("delta", delta)));

    internal void SetNarrowPreviewLayout(bool isNarrow)
    {
        isNarrowPreviewLayout = isNarrow;
        foreach (var row in Rows)
        {
            row.IsNarrowPreviewLayout = isNarrow;
        }
    }

    internal decimal DisplayFrameRate { get; private set; }

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

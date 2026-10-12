using System.Globalization;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains expression diagnostics and chapter row behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private void RefreshRows()
    {
        var frameUpdate = CurrentInfo is null ? null : ClipEditingCoordinator.UpdateFrames(CurrentInfo,
            selectedFrameRateOption, EditingOptions.EffectiveFrameDecimalPlaces,
            FrameAccuracyTolerance);
        appliedFrameRateOption = frameUpdate?.AppliedOption;
        var display = frameUpdate?.FrameResult;
        DisplayFrameRate = display?.Rate.ToDecimal() ?? 0m;
        workspaceContentRows.RefreshRows(Rows, display?.Info, EffectiveFrameRateOption, FramePresentationPolicy);
        SetNarrowPreviewLayout(isNarrowPreviewLayout);
        UpdateInlinePreviewProjection();
    }

    private void UpdateInlinePreviewProjection()
    {
        var preview = pendingContentPreview;
        IReadOnlyDictionary<(ChapterTrackId TrackId, ChapterId ChapterId), ExpressionChapterComparison> comparisons = preview is { IsValid: true }
            && Workspace.IsContentTokenCurrent(preview.BaseToken)
            ? ExpressionPreviewProjector.Build(preview, displayFrameRate: EffectiveFrameRateOption.Rate,
                frameDecimalPlaces: PreviewFrameDecimalPlaces)
                .Chapters.ToDictionary(item => (item.TrackId, item.Id))
            : new Dictionary<(ChapterTrackId TrackId, ChapterId ChapterId), ExpressionChapterComparison>();
        foreach (var row in Rows)
        {
            var comparison = row.TrackId is { } trackId && row.ChapterId is { } chapterId
                && comparisons.TryGetValue((trackId, chapterId), out var mappedComparison)
                    ? mappedComparison
                    : null;
            row.UpdatePreviewComparison(comparison, DisplayFrameRate, Localizer.GetString("Expression.Value.NotCalculated"));
            row.PreviewTimeText = row.HasPreviewTimeChange && row.PreviewComparison?.Candidate is { } candidate
                ? ExpressionChapterRowViewModel.FormatTicks(candidate.StartTicks)
                : string.Empty;
            row.PreviewBeforeTimeText = row.HasPreviewTimeChange && row.PreviewComparison?.Before is { } before
                ? ExpressionChapterRowViewModel.FormatTicks(before.StartTicks)
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
            var beforeInput = row.HasPreviewFramesChange && row.PreviewComparison is { Before: { } beforeChapter, BeforeFrames: { } beforeFrames }
                ? CreateFramePresentationInput(beforeChapter.StartTicks, beforeFrames.FrameRate, row.PreviewBeforeFrames)
                : null;
            var candidateInput = row.HasPreviewFramesChange && row.PreviewComparison is { Candidate: { } candidateChapter, CandidateFrames: { } candidateFrames }
                ? CreateFramePresentationInput(candidateChapter.StartTicks, candidateFrames.FrameRate, row.PreviewFrames)
                : null;
            row.SetPreviewFramePresentationInputs(beforeInput, candidateInput, FramePresentationPolicy);
            if (row.HasPreviewFramesChange)
            {
                var beforeDescription = row.PreviewBeforeFramePresentation?.AccessibleDescription
                    ?? row.PreviewBeforeFrames;
                var candidateDescription = row.PreviewCandidateFramePresentation?.AccessibleDescription
                    ?? row.PreviewFrames;
                row.PreviewFramesAccessibleName = FormatPreviewAccessibleName(
                    Localizer.GetString("Grid.Frames"),
                    $"{Localizer.GetString("Expression.Preview.BeforeFrames")}: {beforeDescription}",
                    $"{Localizer.GetString("Expression.Preview.CandidateFrames")}: {candidateDescription}");
            }
        }
    }

    private FramePresentationInput CreateFramePresentationInput(long startTicks, FrameRateValue rate, string fallback)
    {
        if (selectedFrameRateOption.IsValid && selectedFrameRateOption.Rate.IsKnown)
        {
            return new FramePresentationInput(startTicks, selectedFrameRateOption.Rate, fallback);
        }

        if (rate.IsKnown)
        {
            return new FramePresentationInput(startTicks, rate, fallback);
        }

        return EffectiveFrameRateOption.Rate.IsKnown
            ? new FramePresentationInput(startTicks, EffectiveFrameRateOption.Rate, fallback)
            : FramePresentationInput.FromNumericText(fallback);
    }

    internal FramePresentationPolicy FramePresentationPolicy => new(
        RoundFrames,
        ShowRepeatingFrameDecimals && EditingOptions.FrameDisplay == ChapterTool.Core.Editing.FrameDisplayMode.FullPrecision,
        PreviewFrameDecimalPlaces);

    internal void RefreshFramePresentations()
    {
        var policy = FramePresentationPolicy;
        foreach (var row in Rows)
        {
            row.RefreshFramePresentation(policy);
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

    public FrameRateValue ExpressionFrameRate => ComputationFrameRate;

    internal FrameRateOption SelectedFrameRateOption => selectedFrameRateOption;

    internal FrameRateOption EffectiveFrameRateOption => appliedFrameRateOption ?? selectedFrameRateOption;

    private int PreviewFrameDecimalPlaces => EditingOptions.EffectiveFrameDecimalPlaces;

    internal void RefreshRowsFromPort() => RefreshRows();

}

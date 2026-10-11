using System.Globalization;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Wasm.Services;

/// <summary>Provides one reviewed candidate for the browser main-window content drafts.</summary>
public sealed partial class WasmWorkspace
{
    private const string MainOptionsOperation = "Apply main-window options";

    /// <summary>Gets the main-window candidate, separate from tool-dialog candidates.</summary>
    public ChapterContentPreview? MainOptionsPreview => ContentPreview?.Operation == MainOptionsOperation ? ContentPreview : null;

    /// <summary>Gets the main-window comparison using the active frame display basis.</summary>
    public ExpressionPreviewProjection? MainOptionsPreviewProjection => MainOptionsPreview is null ? null : ContentPreviewProjection;

    /// <summary>Gets whether the main-window candidate needs an explicit refresh.</summary>
    public bool IsMainOptionsPreviewStale => MainOptionsPreview is not null && IsContentPreviewStale;

    /// <summary>Prepares naming, numbering, and expression drafts as one atomic candidate.</summary>
    public ChapterContentPreview? PrepareMainOptionsPreview()
    {
        CancelExpressionPreview();
        var expression = Expression?.Trim() ?? string.Empty;
        var applyExpression = expression.Length > 0 && !IsExpressionAlreadyApplied(expression);
        return PrepareContentPreview(MainOptionsOperation, document =>
        {
            if (OrderShift is < 0 or > 1000)
            {
                return new ChapterCandidateBuildResult(false, document, [], [localizer.T("Parity.InvalidNumber")]);
            }

            var focused = document;
            return candidateBuilder.ApplyOutputOptions(focused, AutoGenerateNames, UseTemplateNames,
                ChapterNameTemplateText ?? string.Empty, OrderShift, applyExpression, expression, SelectedComputationRate);
        }, new HistoryOperationDescriptor("chapter.options-apply",
        [
            new("autoGenerateNames", AutoGenerateNames.ToString(CultureInfo.InvariantCulture), "boolean"),
            new("useTemplateNames", UseTemplateNames.ToString(CultureInfo.InvariantCulture), "boolean"),
            new("orderShift", OrderShift.ToString(CultureInfo.InvariantCulture), "number"),
            new("script", applyExpression ? expression : string.Empty),
            new("trackId", CurrentTrackId?.ToString())
        ]));
    }

    private bool IsExpressionAlreadyApplied(string expression)
    {
        var snapshot = HistorySnapshot;
        if (snapshot is null || CurrentTrackId is not { } trackId)
        {
            return false;
        }
        var nodes = snapshot.Nodes.ToDictionary(static node => node.Id);
        var cursor = snapshot.CursorId;
        while (nodes.TryGetValue(cursor, out var node))
        {
            if (node.Operation is { Kind: "expression.apply" or "chapter.options-apply" } operation
                && operation.Parameters.Any(parameter => parameter.Name == "trackId" && parameter.Value == trackId.ToString())
                && operation.Parameters.FirstOrDefault(static parameter => parameter.Name == "script")?.Value is { Length: > 0 } script)
            {
                return script == expression;
            }
            if (node.ParentId is not { } parentId)
            {
                break;
            }
            cursor = parentId;
        }
        return false;
    }

    /// <summary>Commits the current main-window candidate without recalculating its values.</summary>
    /// <param name="cancellationToken">A token that can cancel submission.</param>
    public ValueTask<bool> ApplyMainOptionsPreviewAsync(CancellationToken cancellationToken = default) =>
        MainOptionsPreview is null ? ValueTask.FromResult(false) : ApplyContentPreviewAsync(cancellationToken);

    /// <summary>Discards the main-window candidate without changing committed content.</summary>
    public void CancelMainOptionsPreview()
    {
        if (MainOptionsPreview is not null)
        {
            CancelContentPreview();
        }
    }

    /// <summary>Formats either side of a candidate with the same policy as the chapter table.</summary>
    /// <param name="row">The reviewed chapter comparison.</param>
    /// <param name="candidate">Whether to format the candidate value.</param>
    /// <param name="showRepeatingDecimals">Whether to mark repeating digits.</param>
    public FramePresentationParts FormatPreviewFramePresentation(ExpressionChapterComparison row, bool candidate, bool showRepeatingDecimals)
    {
        var chapter = candidate ? row.Candidate : row.Before;
        var frames = candidate ? row.CandidateFrames : row.BeforeFrames;
        return FormatFramePresentation(chapter?.StartTicks ?? 0, frames?.FrameRate,
            frames?.Text ?? string.Empty, showRepeatingDecimals);
    }

    /// <summary>Calculates candidate frame accuracy through the shared frame service.</summary>
    /// <param name="ticks">The candidate timestamp ticks.</param>
    public FrameAccuracy GetPreviewFrameAccuracy(long ticks)
    {
        if (!RoundFrames || BaseChapterSet is not { } source || ExactFramesPerSecond <= 0)
        {
            return FrameAccuracy.Neutral;
        }

        var single = source with { Chapters = [new Chapter(1, TimeSpan.FromTicks(ticks), string.Empty)] };
        return frameRateService.UpdateFrames(single, EffectiveFrameRateOption, true, FrameAccuracyTolerance)
            .Info.Chapters[0].FrameAccuracy;
    }
}

using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Exporting;

public sealed class ChapterOutputProjectionService(IExpressionService expressionService)
{
    public ChapterOutputProjectionResult Project(ChapterInfo info, ChapterExportOptions options)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        var expressionResult = new ChapterExpressionService(expressionService).Apply(info, options.ApplyExpression, options.Expression);
        diagnostics.AddRange(expressionResult.Diagnostics);

        var effectiveShift = NormalizeOrderShift(options.OrderShift, diagnostics);
        var templateNames = TemplateNames(options.ChapterNameTemplateText);
        var useGeneratedNames = options.AutoGenerateNames || (options.UseTemplateNames && templateNames.Count == 0);

        var outputIndex = 0;
        var chapters = expressionResult.Info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter with { Number = 0 };
            }

            outputIndex++;
            return chapter with
            {
                Number = outputIndex + effectiveShift,
                Name = OutputName(chapter.Name, outputIndex, useGeneratedNames, templateNames)
            };
        }).ToList();

        return new ChapterOutputProjectionResult(
            expressionResult.Info with { Chapters = chapters },
            chapters.Where(static chapter => !chapter.IsSeparator).ToList(),
            diagnostics);
    }

    private static int NormalizeOrderShift(int orderShift, List<ChapterDiagnostic> diagnostics)
    {
        if (orderShift >= 0)
        {
            return orderShift;
        }

        diagnostics.Add(new ChapterDiagnostic(
            DiagnosticSeverity.Warning,
            "OrderShiftNormalized",
            $"Chapter number shift {orderShift} would produce non-positive chapter numbers and was normalized to 0.",
            Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["shift"] = orderShift }));
        return 0;
    }

    private static List<string> TemplateNames(string templateText) =>
        string.IsNullOrWhiteSpace(templateText)
            ? []
            : templateText
                .Trim(' ', '\r', '\n')
                .Split('\n')
                .Select(static line => line.TrimEnd('\r'))
                .Where(static line => line.Length > 0)
                .ToList();

    private static string OutputName(
        string originalName,
        int outputIndex,
        bool useGeneratedNames,
        IReadOnlyList<string> templateNames)
    {
        if (templateNames.Count >= outputIndex)
        {
            return templateNames[outputIndex - 1];
        }

        return useGeneratedNames ? StandardChapterName(outputIndex) : originalName;
    }

    private static string StandardChapterName(int index) => $"Chapter {index:D2}";
}

public sealed record ChapterOutputProjectionResult(
    ChapterInfo Info,
    IReadOnlyList<Chapter> OutputChapters,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public ChapterOutputProjectionResult(ChapterInfo info, IReadOnlyList<ChapterDiagnostic> diagnostics)
        : this(info, info.Chapters.Where(static chapter => !chapter.IsSeparator).ToList(), diagnostics)
    {
    }
}

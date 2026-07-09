using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using LuaExpressionScriptService = ChapterTool.Core.Transform.Expressions.Lua.LuaExpressionScriptService;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Projects chapter output data before export by applying expressions, ordering, and naming entries.
/// </summary>
public sealed class ChapterOutputProjectionService
{
    private readonly IChapterExpressionEngine expressionEngine;

    /// <summary>
    /// Projects chapter output data before export by applying expressions, ordering, and naming entries.
    /// </summary>
    /// <param name="expressionEngine">The chapter expression engine.</param>
    public ChapterOutputProjectionService(IChapterExpressionEngine? expressionEngine = null)
    {
        this.expressionEngine = expressionEngine ?? new LuaExpressionScriptService();
    }

    /// <summary>
    /// Executes the Project operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="options">The export options.</param>
    /// <returns>The operation result.</returns>
    public ChapterOutputProjectionResult Project(ChapterSet info, ChapterExportOptions options)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        var expressionResult = new ChapterExpressionService(expressionEngine).Apply(info, options.ApplyExpression, options.Expression);
        diagnostics.AddRange(expressionResult.Diagnostics);

        var effectiveShift = NormalizeOrderShift(options.OrderShift, diagnostics);
        var templateNames = TemplateNames(options.ChapterNameTemplateText);
        var useGeneratedNames = options.AutoGenerateNames || (options.UseTemplateNames && templateNames.Count == 0);

        var outputIndex = 0;
        var chapters = expressionResult.Info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter with { DisplayNumber = 0 };
            }

            outputIndex++;
            return chapter with
            {
                DisplayNumber = outputIndex + effectiveShift,
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

/// <summary>
/// Represents projected chapter output and diagnostics.
/// </summary>
/// <param name="Info">The projected chapter set used for export.</param>
/// <param name="OutputChapters">The non-separator chapters included in the exported output.</param>
/// <param name="Diagnostics">Diagnostics produced while projecting export output.</param>
public sealed record ChapterOutputProjectionResult(
    ChapterSet Info,
    IReadOnlyList<Chapter> OutputChapters,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    /// <summary>
    /// Represents projected chapter output and diagnostics.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="diagnostics">The diagnostics for the operation.</param>
    public ChapterOutputProjectionResult(ChapterSet info, IReadOnlyList<ChapterDiagnostic> diagnostics)
        : this(info, info.Chapters.Where(static chapter => !chapter.IsSeparator).ToList(), diagnostics)
    {
    }
}

using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public sealed record ExpressionEvaluationResult(
    bool Success,
    decimal Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);

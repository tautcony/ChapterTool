using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

internal static class FrameRateValidation
{
    public static ChapterDiagnostic InvalidFrameRate() =>
        new(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidFrameRate, "Frame rate must be a finite value greater than zero.");
}

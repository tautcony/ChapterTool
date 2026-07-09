using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

internal static class FrameRateValidation
{
    public static bool TryNormalize(double framesPerSecond, out decimal value, out ChapterDiagnostic? diagnostic)
    {
        value = 0;
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0 || framesPerSecond > (double)decimal.MaxValue)
        {
            diagnostic = InvalidFrameRate();
            return false;
        }

        value = (decimal)framesPerSecond;
        if (value <= 0)
        {
            diagnostic = InvalidFrameRate();
            return false;
        }

        diagnostic = null;
        return true;
    }

    public static ChapterDiagnostic InvalidFrameRate() =>
        new(DiagnosticSeverity.Error, "InvalidFrameRate", "Frame rate must be a finite value greater than zero.");
}

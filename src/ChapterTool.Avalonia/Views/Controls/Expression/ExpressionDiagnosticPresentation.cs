using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls.Expression;

/// <summary>Draws diagnostic underlines using the theme diagnostic error brush.</summary>
public sealed class ExpressionDiagnosticPresentation : IBackgroundRenderer
{
    private IReadOnlyList<ExpressionAuthoringDiagnostic> diagnostics = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Update(IReadOnlyList<ExpressionAuthoringDiagnostic> value) => diagnostics = value;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        textView.EnsureVisualLines();
        foreach (var diagnostic in diagnostics.Where(static diagnostic => diagnostic.Length > 0))
        {
            var rects = BackgroundGeometryBuilder.GetRectsForSegment(
                textView,
                new Segment(diagnostic.Start, diagnostic.Length),
                false);

            foreach (var rect in rects)
            {
                DrawUnderline(drawingContext, rect);
            }
        }
    }

    private static void DrawUnderline(DrawingContext drawingContext, Rect rect)
    {
        var pen = new Pen(ExpressionThemeBrushes.DiagnosticUnderline, 1);
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var startX = rect.X;
        var y = rect.Bottom - 1;
        var step = 4d;
        var up = true;

        context.BeginFigure(new Point(startX, y), false);
        for (var x = startX + step; x <= rect.Right; x += step)
        {
            context.LineTo(new Point(x, y + (up ? -2 : 0)));
            up = !up;
        }

        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private sealed record Segment(int Offset, int Length) : ISegment
    {
        public int EndOffset => Offset + Length;
    }
}

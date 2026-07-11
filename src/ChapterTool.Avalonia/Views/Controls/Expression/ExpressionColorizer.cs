using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls.Expression;

/// <summary>Token-span colorizer for the expression editor; brushes come from theme resources.</summary>
public sealed class ExpressionColorizer(IReadOnlyList<ExpressionTokenSpan> spans) : DocumentColorizingTransformer
{
    private IReadOnlyList<ExpressionTokenSpan> spans = spans;

    public void Update(IReadOnlyList<ExpressionTokenSpan> value) => spans = value;

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        foreach (var span in spans)
        {
            var start = span.Start;
            var end = span.Start + span.Length;
            if (end <= lineStart || start >= lineEnd)
            {
                continue;
            }

            ChangeLinePart(
                Math.Max(start, lineStart),
                Math.Min(end, lineEnd),
                element => element.TextRunProperties.SetForegroundBrush(ExpressionThemeBrushes.ForTokenKind(span.Kind)));
        }
    }
}

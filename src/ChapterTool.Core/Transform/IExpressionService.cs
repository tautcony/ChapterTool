namespace ChapterTool.Core.Transform;

public interface IExpressionService
{
    ExpressionEvaluationResult EvaluateInfix(string expression, decimal timeSeconds, decimal framesPerSecond);

    ExpressionEvaluationResult EvaluatePostfix(IEnumerable<string> tokens, decimal timeSeconds, decimal framesPerSecond);
}

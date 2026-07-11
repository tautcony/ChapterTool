using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.Session;

/// <summary>
/// Workspace-owned projection surface: naming mode, order shift, expression session fields,
/// and last-successful expression projection cache.
/// </summary>
public sealed class ProjectionState
{
    public bool AutoGenerateNames { get; private set; }

    public bool UseTemplateNames { get; private set; }

    public string ChapterNameTemplateText { get; private set; } = string.Empty;

    public int OrderShift { get; private set; }

    public bool ApplyExpression { get; private set; }

    public string Expression { get; private set; } = "t";

    public string ExpressionPresetId { get; private set; } = string.Empty;

    public string ExpressionSourceName { get; private set; } = string.Empty;

    public ChapterOutputProjectionResult? LastSuccessfulExpressionProjection { get; set; }

    /// <summary>
    /// Sets auto-generate naming mode. Mutually exclusive with template names.
    /// Returns whether any naming field changed.
    /// </summary>
    public bool SetAutoGenerateNames(bool value)
    {
        if (AutoGenerateNames == value)
        {
            return false;
        }

        AutoGenerateNames = value;
        if (value && UseTemplateNames)
        {
            UseTemplateNames = false;
        }

        return true;
    }

    /// <summary>
    /// Sets template naming mode. Mutually exclusive with auto-generate.
    /// Returns whether any naming field changed.
    /// </summary>
    public bool SetUseTemplateNames(bool value)
    {
        if (UseTemplateNames == value)
        {
            return false;
        }

        UseTemplateNames = value;
        if (value && AutoGenerateNames)
        {
            AutoGenerateNames = false;
        }

        return true;
    }

    public bool SetChapterNameTemplateText(string value)
    {
        value ??= string.Empty;
        if (string.Equals(ChapterNameTemplateText, value, StringComparison.Ordinal))
        {
            return false;
        }

        ChapterNameTemplateText = value;
        return true;
    }

    public bool SetOrderShift(int value)
    {
        if (OrderShift == value)
        {
            return false;
        }

        OrderShift = value;
        return true;
    }

    public bool SetApplyExpression(bool value)
    {
        if (ApplyExpression == value)
        {
            return false;
        }

        ApplyExpression = value;
        return true;
    }

    public bool SetExpression(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "t" : value;
        if (string.Equals(Expression, value, StringComparison.Ordinal))
        {
            return false;
        }

        Expression = value;
        return true;
    }

    public bool SetExpressionPresetId(string value)
    {
        value ??= string.Empty;
        if (string.Equals(ExpressionPresetId, value, StringComparison.Ordinal))
        {
            return false;
        }

        ExpressionPresetId = value;
        return true;
    }

    public bool SetExpressionSourceName(string value)
    {
        value ??= string.Empty;
        if (string.Equals(ExpressionSourceName, value, StringComparison.Ordinal))
        {
            return false;
        }

        ExpressionSourceName = value;
        return true;
    }

    /// <summary>
    /// Atomically updates expression-related fields so callers can refresh rows once.
    /// </summary>
    public void ApplyExpressionFields(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName)
    {
        Expression = string.IsNullOrWhiteSpace(expression) ? "t" : expression;
        ApplyExpression = applyExpression;
        ExpressionPresetId = expressionPresetId ?? string.Empty;
        ExpressionSourceName = expressionSourceName ?? string.Empty;
    }

    public void ClearProjectionCache() => LastSuccessfulExpressionProjection = null;
}

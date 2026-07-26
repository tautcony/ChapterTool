using ChapterTool.Core.Exporting;

namespace ChapterTool.Core.Session;

/// <summary>
/// Workspace-owned projection surface: naming mode, order shift, expression session fields,
/// and last-successful expression projection cache.
/// </summary>
public sealed class ProjectionState
{
    /// <summary>Gets a value indicating whether chapter names are auto-generated.</summary>
    public bool AutoGenerateNames { get; private set; }

    /// <summary>Gets a value indicating whether chapter names come from a template.</summary>
    public bool UseTemplateNames { get; private set; }

    /// <summary>Gets the multi-line chapter name template text.</summary>
    public string ChapterNameTemplateText { get; private set; } = string.Empty;

    /// <summary>Gets the chapter number order shift applied during projection.</summary>
    public int OrderShift { get; private set; }

    /// <summary>Gets a value indicating whether the expression is applied during projection.</summary>
    public bool ApplyExpression { get; private set; }

    /// <summary>Gets the active chapter time expression text.</summary>
    public string Expression { get; private set; } = "t";

    /// <summary>Gets the selected expression preset identifier, when any.</summary>
    public string ExpressionPresetId { get; private set; } = string.Empty;

    /// <summary>Gets the display name of the expression source (preset, file, or manual).</summary>
    public string ExpressionSourceName { get; private set; } = string.Empty;

    /// <summary>Gets or sets the last successful expression projection retained for mid-edit invalid expressions.</summary>
    public ChapterOutputProjectionResult? LastSuccessfulExpressionProjection { get; set; }

    /// <summary>
    /// Sets auto-generate naming mode. Mutually exclusive with template names.
    /// Returns whether any naming field changed.
    /// </summary>
    /// <param name="value">Whether auto-generate names is enabled.</param>
    /// <returns><see langword="true"/> when any naming field changed; otherwise <see langword="false"/>.</returns>
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
    /// <param name="value">Whether template names are enabled.</param>
    /// <returns><see langword="true"/> when any naming field changed; otherwise <see langword="false"/>.</returns>
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

    /// <summary>Sets the chapter name template text. Returns whether the value changed.</summary>
    /// <param name="value">The new template text.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetChapterNameTemplateText(string? value)
    {
        value ??= string.Empty;
        if (string.Equals(ChapterNameTemplateText, value, StringComparison.Ordinal))
        {
            return false;
        }

        ChapterNameTemplateText = value;
        return true;
    }

    /// <summary>Sets the order shift. Returns whether the value changed.</summary>
    /// <param name="value">The new order shift.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetOrderShift(int value)
    {
        if (OrderShift == value)
        {
            return false;
        }

        OrderShift = value;
        return true;
    }

    /// <summary>Sets whether the expression is applied. Returns whether the value changed.</summary>
    /// <param name="value">Whether the expression is applied.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetApplyExpression(bool value)
    {
        if (ApplyExpression == value)
        {
            return false;
        }

        ApplyExpression = value;
        return true;
    }

    /// <summary>Sets the expression text. Returns whether the value changed.</summary>
    /// <param name="value">The new expression text.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
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

    /// <summary>Sets the expression preset identifier. Returns whether the value changed.</summary>
    /// <param name="value">The new preset identifier.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetExpressionPresetId(string? value)
    {
        value ??= string.Empty;
        if (string.Equals(ExpressionPresetId, value, StringComparison.Ordinal))
        {
            return false;
        }

        ExpressionPresetId = value;
        return true;
    }

    /// <summary>Sets the expression source display name. Returns whether the value changed.</summary>
    /// <param name="value">The new source name.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise <see langword="false"/>.</returns>
    public bool SetExpressionSourceName(string? value)
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
    /// <param name="expression">The expression text.</param>
    /// <param name="applyExpression">Whether the expression is applied.</param>
    /// <param name="expressionPresetId">The preset identifier, when any.</param>
    /// <param name="expressionSourceName">The source display name.</param>
    public void ApplyExpressionFields(
        string expression,
        bool applyExpression,
        string? expressionPresetId,
        string? expressionSourceName)
    {
        Expression = string.IsNullOrWhiteSpace(expression) ? "t" : expression;
        ApplyExpression = applyExpression;
        ExpressionPresetId = expressionPresetId ?? string.Empty;
        ExpressionSourceName = expressionSourceName ?? string.Empty;
    }

    /// <summary>Clears the last successful expression projection cache.</summary>
    public void ClearProjectionCache() => LastSuccessfulExpressionProjection = null;
}

using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Evaluates conditional compilation directives from the JSON definitions.
/// </summary>
public sealed class ConditionalEvaluator
{
    private readonly Dictionary<string, bool> _knownDefines;

    public ConditionalEvaluator(Dictionary<string, bool> knownDefines)
    {
        _knownDefines = knownDefines;
    }

    /// <summary>
    /// Evaluates whether an item with the given conditionals should be included.
    /// Returns true if the item should be included.
    /// </summary>
    public bool ShouldInclude(List<ConditionalItem>? conditionals)
    {
        if (conditionals == null || conditionals.Count == 0)
            return true;

        foreach (var cond in conditionals)
        {
            switch (cond.Condition)
            {
                case "ifdef":
                    if (!IsExpressionTrue(cond.Expression)) return false;
                    break;
                case "ifndef":
                    if (IsExpressionTrue(cond.Expression)) return false;
                    break;
                case "if":
                    // For complex #if expressions, evaluate what we can.
                    // If the expression is unknown, exclude the item to avoid duplicates.
                    if (!EvaluateIfExpression(cond.Expression)) return false;
                    break;
                case "ifnot":
                    // Inverse of "if" — this is the #else branch.
                    if (EvaluateIfExpression(cond.Expression)) return false;
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a simple define name is considered true.
    /// </summary>
    private bool IsExpressionTrue(string expression)
    {
        return _knownDefines.TryGetValue(expression, out var val) && val;
    }

    /// <summary>
    /// Evaluates a complex #if expression. Returns false for unknown/platform-specific expressions
    /// to pick the portable branch (the "ifnot"/else branch).
    /// </summary>
    private bool EvaluateIfExpression(string expression)
    {
        // Check if this matches a simple known define
        if (_knownDefines.TryGetValue(expression, out var val))
            return val;

        // For compound expressions like "defined(_MSC_VER)&&!defined(__clang__)",
        // return false so the portable (non-MSVC) branch is chosen.
        return false;
    }
}

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
            var isDefined = _knownDefines.TryGetValue(cond.Expression, out var val) && val;

            switch (cond.Condition)
            {
                case "ifdef":
                    if (!isDefined) return false;
                    break;
                case "ifndef":
                    if (isDefined) return false;
                    break;
            }
        }

        return true;
    }
}

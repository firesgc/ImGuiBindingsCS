using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates C# constants from #define macros.
/// </summary>
public sealed class ConstantGenerator
{
    private readonly GeneratorConfig _config;
    private readonly ConditionalEvaluator _condEval;
    private readonly HashSet<string> _emittedNames;

    public ConstantGenerator(GeneratorConfig config, ConditionalEvaluator condEval, HashSet<string>? emittedNames = null)
    {
        _config = config;
        _condEval = condEval;
        _emittedNames = emittedNames ?? [];
    }

    public void Generate(CodeWriter w, List<DefineItem> defines)
    {
        w.WriteLine("public static partial class ImGuiConstants");
        w.OpenBrace();

        foreach (var define in defines)
        {
            if (!_condEval.ShouldInclude(define.Conditionals))
                continue;

            if (string.IsNullOrEmpty(define.Content))
                continue;

            var name = define.Name;

            // Skip already-emitted constants (from other JSON files)
            if (!_emittedNames.Add(name))
                continue;

            var content = define.Content.Trim();

            // Skip macros that are not simple values
            if (content.Contains('(') && !content.StartsWith('('))
                continue;

            // Skip runtime expressions that cannot be C# const
            // (ternary operators, member access, function calls)
            if (IsRuntimeExpression(content))
                continue;

            // Determine value type and emit
            var (csType, csValue) = ParseDefineValue(content);
            if (csType == null)
                continue;

            w.WriteLine($"public const {csType} {name} = {csValue};");
        }

        w.CloseBrace();
    }

    /// <summary>
    /// Checks whether the define content contains runtime expressions
    /// that cannot be represented as a C# const.
    /// </summary>
    private static bool IsRuntimeExpression(string content)
    {
        // Ternary operator: "cond ? a : b"
        if (content.Contains('?') && content.Contains(':'))
            return true;

        // Member access (e.g., "g.IO.Config...")
        // But don't filter out float literals like "3.14f"
        if (content.Contains('.') && !IsNumericLiteral(content))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a string looks like a numeric literal (integer, float, hex).
    /// </summary>
    private static bool IsNumericLiteral(string s)
    {
        s = s.TrimEnd('f', 'F', 'L', 'l', 'U', 'u');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return s[2..].All(c => char.IsAsciiHexDigit(c));
        return s.All(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E');
    }

    private static (string? Type, string Value) ParseDefineValue(string content)
    {
        // String literal: "1.92.7"
        if (content.StartsWith('"') && content.EndsWith('"'))
            return ("string", content);

        // Try parse as integer
        if (TryParseInteger(content, out var intVal))
            return ("int", intVal.ToString());

        // Hex literal
        if (content.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            // Check if it fits in int (may be unsigned)
            if (content.Length <= 10) // 0x + 8 hex digits
                return ("int", $"unchecked((int){content})");
            return ("long", $"unchecked((long){content})");
        }

        // Skip values that reference other macros/identifiers (not resolvable as constants)
        // e.g., IM_COUNTOF, IM_ARRAYSIZE = IM_COUNTOF
        // Check this early to avoid false matches (e.g., identifier ending with 'F' matching float check)
        if (content.All(c => char.IsLetterOrDigit(c) || c == '_') && content.Any(char.IsLetter))
        {
            // It's a single identifier — skip it as it's a macro alias
            return (null, content);
        }

        // Float literal
        if (content.EndsWith('f') || content.EndsWith('F') || content.Contains('.'))
        {
            return ("float", content.EndsWith('f') || content.EndsWith('F') ? content : content + "f");
        }

        // Empty define or flag-style (no value)
        if (content is "" or "1")
            return (null, "");

        return (null, content);
    }

    private static bool TryParseInteger(string s, out long value)
    {
        // Handle parenthesized expressions
        s = s.Trim();
        if (s.StartsWith('(') && s.EndsWith(')'))
            s = s[1..^1].Trim();

        return long.TryParse(s, out value);
    }
}

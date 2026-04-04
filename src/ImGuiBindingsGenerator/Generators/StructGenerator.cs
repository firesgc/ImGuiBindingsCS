using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates C# struct definitions from ImGui struct definitions.
/// Produces unsafe structs with [StructLayout(LayoutKind.Sequential)] for P/Invoke interop.
/// </summary>
public sealed class StructGenerator
{
    private readonly GeneratorConfig _config;
    private readonly TypeResolver _typeResolver;
    private readonly ConditionalEvaluator _condEval;

    // Collects inline array types that need to be generated
    private readonly List<InlineArrayInfo> _inlineArrays = [];

    // Collects delegate types found in struct fields
    private readonly List<DelegateInfo> _fieldDelegates = [];

    public IReadOnlyList<InlineArrayInfo> InlineArrays => _inlineArrays;
    public IReadOnlyList<DelegateInfo> FieldDelegates => _fieldDelegates;

    public StructGenerator(GeneratorConfig config, TypeResolver typeResolver, ConditionalEvaluator condEval)
    {
        _config = config;
        _typeResolver = typeResolver;
        _condEval = condEval;
    }

    public void Generate(CodeWriter w, List<StructItem> structs)
    {
        foreach (var structDef in structs)
        {
            if (!_condEval.ShouldInclude(structDef.Conditionals))
                continue;

            // Skip forward declarations (opaque types) — generate as empty structs
            if (structDef.ForwardDeclaration)
            {
                GenerateOpaqueStruct(w, structDef);
                w.WriteLine();
                continue;
            }

            // Skip anonymous types
            if (structDef.IsAnonymous)
                continue;

            GenerateStruct(w, structDef);
            w.WriteLine();
        }
    }

    private void GenerateOpaqueStruct(CodeWriter w, StructItem structDef)
    {
        var csName = _config.StripStructTypePrefix(structDef.Name);
        w.WriteDocComment(
            structDef.Comments?.Preceding,
            structDef.Comments?.Attached);
        w.WriteLine("[StructLayout(LayoutKind.Sequential)]");
        w.WriteLine($"public partial struct {csName}");
        w.OpenBrace();
        w.CloseBrace();
    }

    private void GenerateStruct(CodeWriter w, StructItem structDef)
    {
        var csName = _config.StripStructTypePrefix(structDef.Name);
        bool hasUnsafe = false;

        // Pre-scan for unsafe fields
        foreach (var field in structDef.Fields)
        {
            if (!_condEval.ShouldInclude(field.Conditionals))
                continue;

            if (field.IsArray || FieldRequiresUnsafe(field))
            {
                hasUnsafe = true;
                break;
            }
        }

        w.WriteDocComment(
            structDef.Comments?.Preceding,
            structDef.Comments?.Attached);
        w.WriteLine("[StructLayout(LayoutKind.Sequential)]");
        var unsafeModifier = hasUnsafe ? "unsafe " : "";
        w.WriteLine($"public {unsafeModifier}partial struct {csName}");
        w.OpenBrace();

        // Use aligned block for struct fields so trailing comments line up
        w.BeginAlignedBlock();

        foreach (var field in structDef.Fields)
        {
            if (!_condEval.ShouldInclude(field.Conditionals))
                continue;

            GenerateField(w, field, structDef.Name);
        }

        w.EndAlignedBlock();
        w.CloseBrace();
    }

    private void GenerateField(CodeWriter w, StructField field, string structName)
    {
        var csFieldName = TypeMapper.EscapeIdentifier(field.Name);
        var comment = CodeWriter.CleanTrailingComment(field.Comments?.Attached);

        // Handle function pointer fields
        if (field.Type.TypeDetails?.Flavour == "function_pointer")
        {
            // Emit as nint (function pointer stored as IntPtr)
            w.WriteLineWithComment($"public nint {csFieldName};", comment);
            // Also collect delegate info
            var delegateInfo = _typeResolver.ResolveFieldFunctionPointer(field);
            if (delegateInfo != null)
            {
                var prefixedName = $"{_config.StripStructTypePrefix(structName)}_{field.Name}_delegate";
                _fieldDelegates.Add(delegateInfo with { Name = prefixedName });
            }
            return;
        }

        // Handle arrays
        if (field.IsArray && field.ArrayBounds != null)
        {
            GenerateArrayField(w, field, csFieldName, comment, structName);
            return;
        }

        // Regular field
        var csType = _typeResolver.Resolve(field.Type);
        w.WriteLineWithComment($"public {csType} {csFieldName};", comment);
    }

    private void GenerateArrayField(CodeWriter w, StructField field, string csFieldName, string? comment, string structName)
    {
        var elementType = _typeResolver.Resolve(field.Type.Description?.InnerType ?? field.Type.Description);
        var bounds = field.ArrayBounds!;

        // Try to resolve the bounds to an integer
        if (TryResolveBounds(bounds, out var size))
        {
            if (TypeMapper.IsFixedBufferCompatible(elementType))
            {
                // Use fixed keyword for primitive types
                w.WriteLineWithComment($"public fixed {elementType} {csFieldName}[{size}];", comment);
            }
            else
            {
                // Use InlineArray attribute for complex types
                var inlineArrayName = $"{_config.StripStructTypePrefix(structName)}_{csFieldName}_Array";
                _inlineArrays.Add(new InlineArrayInfo(inlineArrayName, elementType, size));
                w.WriteLineWithComment($"public {inlineArrayName} {csFieldName};", comment);
            }
        }
        else
        {
            // Bounds is a constant expression we can't resolve - use InlineArray with the expression
            var inlineArrayName = $"{_config.StripStructTypePrefix(structName)}_{csFieldName}_Array";
            // We'll need to emit these with a comment noting the expression
            _inlineArrays.Add(new InlineArrayInfo(inlineArrayName, elementType, 0, bounds));
            w.WriteLineWithComment($"public {inlineArrayName} {csFieldName};", comment);
        }
    }

    private bool FieldRequiresUnsafe(StructField field)
    {
        var csType = _typeResolver.Resolve(field.Type);
        return TypeResolver.ContainsPointer(csType);
    }

    /// <summary>
    /// Well-known constant expressions used in array bounds.
    /// </summary>
    private static readonly Dictionary<string, int> KnownConstants = new()
    {
        ["ImGuiCol_COUNT"] = 58,
        ["ImGuiKey_COUNT"] = 666,
        ["ImGuiKey_NamedKey_COUNT"] = 154,
        ["IM_DRAWLIST_TEX_LINES_WIDTH_MAX"] = 63,
        ["IM_UNICODE_CODEPOINT_MAX"] = 0x10FFFF,
    };

    private static bool TryResolveBounds(string bounds, out int result)
    {
        bounds = bounds.Trim();

        // Direct integer
        if (int.TryParse(bounds, out result))
            return true;

        // Known constant
        if (KnownConstants.TryGetValue(bounds, out result))
            return true;

        // Simple expression like "32+1"
        if (bounds.Contains('+') && !bounds.Contains('('))
        {
            var parts = bounds.Split('+');
            if (parts.Length == 2 &&
                TryResolveBounds(parts[0].Trim(), out var a) &&
                TryResolveBounds(parts[1].Trim(), out var b))
            {
                result = a + b;
                return true;
            }
        }

        // Expression with known constants like (IM_UNICODE_CODEPOINT_MAX+1)/8192/8
        // Try simple evaluation
        var expr = bounds;
        foreach (var (key, val) in KnownConstants)
        {
            expr = expr.Replace(key, val.ToString());
        }

        // Very basic expression evaluation for /+*- only
        try
        {
            // Remove spaces and parentheses, evaluate simple math
            expr = expr.Replace(" ", "");
            if (TryEvaluateSimpleExpr(expr, out result))
                return true;
        }
        catch
        {
            // Fall through
        }

        result = 0;
        return false;
    }

    private static bool TryEvaluateSimpleExpr(string expr, out int result)
    {
        result = 0;

        // Handle parentheses by removing them (only works for simple cases)
        while (expr.Contains('('))
        {
            var close = expr.IndexOf(')');
            if (close < 0) return false;
            var open = expr.LastIndexOf('(', close);
            if (open < 0) return false;

            var inner = expr[(open + 1)..close];
            if (!TryEvaluateSimpleExpr(inner, out var innerResult))
                return false;

            expr = string.Concat(expr.AsSpan(0, open), innerResult.ToString(), expr.AsSpan(close + 1));
        }

        // Split by + and - (respecting operator precedence: * and / first)
        var terms = SplitByAddSub(expr);
        result = 0;
        foreach (var (sign, term) in terms)
        {
            if (!TryEvaluateMulDiv(term, out var termResult))
                return false;
            result += sign * termResult;
        }
        return true;
    }

    private static List<(int Sign, string Term)> SplitByAddSub(string expr)
    {
        var terms = new List<(int Sign, string Term)>();
        int sign = 1;
        int start = 0;

        for (int i = 0; i < expr.Length; i++)
        {
            if (i > start && (expr[i] == '+' || expr[i] == '-'))
            {
                terms.Add((sign, expr[start..i]));
                sign = expr[i] == '-' ? -1 : 1;
                start = i + 1;
            }
        }
        terms.Add((sign, expr[start..]));
        return terms;
    }

    private static bool TryEvaluateMulDiv(string expr, out int result)
    {
        result = 0;
        if (string.IsNullOrEmpty(expr)) return false;

        // Split by * and /
        var parts = new List<(char Op, string Val)>();
        int start = 0;
        parts.Add(('*', ""));

        for (int i = 0; i < expr.Length; i++)
        {
            if (i > start && (expr[i] == '*' || expr[i] == '/'))
            {
                parts[^1] = (parts[^1].Op, expr[start..i]);
                parts.Add((expr[i], ""));
                start = i + 1;
            }
        }
        parts[^1] = (parts[^1].Op, expr[start..]);

        result = 1;
        foreach (var (op, val) in parts)
        {
            if (!int.TryParse(val, out var v))
                return false;
            if (op == '*') result *= v;
            else if (op == '/') result /= v;
        }
        return true;
    }
}

/// <summary>
/// Information about an [InlineArray] type to generate.
/// </summary>
public sealed record InlineArrayInfo(string Name, string ElementType, int Size, string? SizeExpression = null);

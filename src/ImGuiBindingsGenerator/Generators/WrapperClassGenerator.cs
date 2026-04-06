using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates a high-level C# wrapper class that forwards calls to the raw P/Invoke class
/// (e.g., ImGuiNative) with C# default parameter values where possible.
/// This gives users a cleaner API: ImGui.Begin("Window") instead of ImGuiNative.Begin("Window", ...).
/// </summary>
public sealed class WrapperClassGenerator
{
    private readonly GeneratorConfig _config;
    private readonly TypeResolver _typeResolver;
    private readonly ConditionalEvaluator _condEval;

    public WrapperClassGenerator(GeneratorConfig config, TypeResolver typeResolver, ConditionalEvaluator condEval)
    {
        _config = config;
        _typeResolver = typeResolver;
        _condEval = condEval;
    }

    public void Generate(CodeWriter w, List<FunctionItem> functions, string wrapperClassName, string nativeClassName)
    {
        w.WriteLine($"public static unsafe partial class {wrapperClassName}");
        w.OpenBrace();

        foreach (var func in functions)
        {
            if (!ShouldInclude(func))
                continue;

            GenerateWrapperMethod(w, func, nativeClassName);
            w.WriteLine();
        }

        w.CloseBrace();
    }

    private bool ShouldInclude(FunctionItem func)
    {
        if (!_condEval.ShouldInclude(func.Conditionals))
            return false;

        if (_config.SkipImStrHelpers && func.IsImstrHelper)
            return false;

        if (_config.SkipVarargsFunctions)
        {
            if (func.Arguments.Any(a => a.IsVarargs))
                return false;
            if (func.Arguments.Any(a => a.Type?.Declaration?.Contains("va_list") == true))
                return false;
        }

        return true;
    }

    private void GenerateWrapperMethod(CodeWriter w, FunctionItem func, string nativeClassName)
    {
        // Resolve return type (same logic as FunctionGenerator)
        var returnType = func.ReturnType != null
            ? _typeResolver.Resolve(func.ReturnType)
            : "void";

        var returnIsBool = TypeResolver.IsBoolType(func.ReturnType);
        if (returnIsBool)
            returnType = "bool";

        // Build parameter descriptors
        var parameters = BuildParameters(func);

        // Apply C# default values to the trailing consecutive parameters that support them
        ApplyTrailingDefaults(parameters);

        var csMethodName = _config.StripFunctionPrefix(func.Name);

        // Write documentation
        w.WriteDocComment(func.Comments?.Preceding, func.Comments?.Attached);

        // Build the signature and call argument lists
        var paramList = string.Join(", ", parameters.Select(p => p.ToSignatureString()));
        var argList = string.Join(", ", parameters.Select(p => p.ToCallString()));

        // Emit as expression-bodied method
        w.WriteLine($"public static {returnType} {csMethodName}({paramList})");
        w.WriteLine($"    => {nativeClassName}.{csMethodName}({argList});");
    }

    private List<WrapperParam> BuildParameters(FunctionItem func)
    {
        var parameters = new List<WrapperParam>();

        foreach (var arg in func.Arguments)
        {
            if (arg.IsVarargs)
                continue;

            var paramName = TypeMapper.EscapeIdentifier(arg.Name ?? $"arg{parameters.Count}");
            string csType;
            string refKind = ""; // "", "ref ", "out "
            bool isNullableString = false;

            if (TypeResolver.IsConstCharPointer(arg.Type))
            {
                csType = "string";
                // If default is NULL, make it nullable
                if (arg.DefaultValue == "NULL")
                    isNullableString = true;
            }
            else if (TypeResolver.IsBoolType(arg.Type))
            {
                csType = "bool";
            }
            else if (TypeResolver.IsBoolPointer(arg.Type))
            {
                csType = "bool";
                refKind = "ref ";
            }
            else if (TypeResolver.GetBlittableBuiltinPointerType(arg.Type) is { } refType)
            {
                csType = refType;
                refKind = "ref ";
            }
            else
            {
                csType = arg.Type != null ? _typeResolver.Resolve(arg.Type) : "nint";
            }

            // Try to translate the C default value to a C# default value
            string? csDefault = TranslateDefaultValue(arg.DefaultValue, csType, refKind, isNullableString);

            parameters.Add(new WrapperParam(paramName, csType, refKind, csDefault, isNullableString));
        }

        return parameters;
    }

    /// <summary>
    /// C# requires that all optional parameters come after all required parameters.
    /// Walk backwards from the end and keep defaults only on the trailing consecutive
    /// run of parameters that have translatable defaults.
    /// </summary>
    private static void ApplyTrailingDefaults(List<WrapperParam> parameters)
    {
        for (int i = parameters.Count - 1; i >= 0; i--)
        {
            if (parameters[i].CsDefault == null)
            {
                // This parameter has no default — clear defaults on ALL preceding params too
                for (int j = i; j >= 0; j--)
                {
                    parameters[j] = parameters[j] with { CsDefault = null, IsNullableString = false };
                }
                break;
            }
        }
    }

    /// <summary>
    /// Translates a C default value to a C# default parameter value expression.
    /// Returns null if the default value cannot be expressed in C#.
    /// </summary>
    private static string? TranslateDefaultValue(string? cDefault, string csType, string refKind, bool isNullableString)
    {
        if (cDefault == null)
            return null;

        // ref parameters cannot have default values in C#
        if (refKind.Length > 0)
            return null;

        // Pointer types can default to null in unsafe context
        if (csType.EndsWith('*'))
            return cDefault == "NULL" ? "null" : null;

        // NULL handling
        if (cDefault == "NULL")
        {
            // string → null (with nullable type)
            if (csType == "string")
                return "null";

            // nint → 0 (null pointer equivalent)
            if (csType == "nint")
                return "0";

            return null;
        }

        // Boolean defaults
        if (csType == "bool")
        {
            return cDefault switch
            {
                "true" => "true",
                "false" => "false",
                _ => null,
            };
        }

        // Float defaults
        if (csType == "float")
        {
            if (cDefault == "FLT_MAX") return "float.MaxValue";
            if (cDefault == "-FLT_MIN") return "-float.Epsilon";
            // Accept float literals like "0.0f", "1.0f", "-1.0f"
            if (cDefault.EndsWith('f') && float.TryParse(cDefault.AsSpan(0, cDefault.Length - 1),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                return cDefault;
            // Accept plain numbers and add 'f' suffix
            if (float.TryParse(cDefault, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return cDefault + "f";
            return null;
        }

        // Double defaults
        if (csType == "double")
        {
            if (double.TryParse(cDefault, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return cDefault;
            return null;
        }

        // ImVec2 defaults — only support zero vectors as 'default'
        if (csType == "ImVec2")
        {
            return cDefault is "ImVec2(0, 0)" or "ImVec2(0.0f, 0.0f)" ? "default" : null;
        }

        // ImVec4 defaults — only support zero vectors as 'default'
        if (csType == "ImVec4")
        {
            return cDefault is "ImVec4(0, 0, 0, 0)" ? "default" : null;
        }

        // String literal defaults
        if (csType == "string" && cDefault.StartsWith('"') && cDefault.EndsWith('"'))
        {
            return cDefault;
        }

        // Integer/enum numeric defaults
        if (csType is "int" or "uint" or "short" or "ushort" or "byte" or "sbyte"
            or "long" or "ulong" or "nuint" or "nint")
        {
            if (long.TryParse(cDefault, out _))
                return cDefault;
            return null;
        }

        // For enum types and other user types, try parsing as integer (flags = 0, etc.)
        if (int.TryParse(cDefault, out _))
            return cDefault;

        return null;
    }

    /// <summary>
    /// Represents a parameter in the wrapper method signature.
    /// </summary>
    private sealed record WrapperParam(
        string Name,
        string CsType,
        string RefKind,
        string? CsDefault,
        bool IsNullableString)
    {
        /// <summary>The translated C# default value, or null if no default.</summary>
        public string? CsDefault { get; set; } = CsDefault;

        /// <summary>Whether the string type should be nullable (string?).</summary>
        public bool IsNullableString { get; set; } = IsNullableString;

        /// <summary>
        /// Builds the parameter declaration for the method signature.
        /// E.g., "string name", "ref bool p_open", "WindowFlags flags = 0"
        /// </summary>
        public string ToSignatureString()
        {
            var type = CsType;
            if (IsNullableString && CsDefault == "null")
                type = "string?";

            var defaultSuffix = CsDefault != null ? $" = {CsDefault}" : "";
            return $"{RefKind}{type} {Name}{defaultSuffix}";
        }

        /// <summary>
        /// Builds the argument expression for the forwarding call.
        /// E.g., "name", "ref p_open", "flags"
        /// </summary>
        public string ToCallString()
        {
            return $"{RefKind}{Name}";
        }
    }
}

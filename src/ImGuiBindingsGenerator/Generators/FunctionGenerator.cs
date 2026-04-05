using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates C# P/Invoke function declarations from ImGui function definitions.
/// </summary>
public sealed class FunctionGenerator
{
    private readonly GeneratorConfig _config;
    private readonly TypeResolver _typeResolver;
    private readonly ConditionalEvaluator _condEval;

    public FunctionGenerator(GeneratorConfig config, TypeResolver typeResolver, ConditionalEvaluator condEval)
    {
        _config = config;
        _typeResolver = typeResolver;
        _condEval = condEval;
    }

    public void Generate(CodeWriter w, List<FunctionItem> functions)
    {
        w.WriteLine($"public static unsafe partial class ImGuiNative");
        w.OpenBrace();

        foreach (var func in functions)
        {
            if (!ShouldInclude(func))
                continue;

            GenerateFunction(w, func);
            w.WriteLine();
        }

        w.CloseBrace();
    }

    private bool ShouldInclude(FunctionItem func)
    {
        if (!_condEval.ShouldInclude(func.Conditionals))
            return false;

        // Skip ImStr helper variants
        if (_config.SkipImStrHelpers && func.IsImstrHelper)
            return false;

        // Skip varargs functions (both is_varargs marker and va_list parameter type)
        if (_config.SkipVarargsFunctions)
        {
            if (func.Arguments.Any(a => a.IsVarargs))
                return false;
            if (func.Arguments.Any(a => a.Type?.Declaration?.Contains("va_list") == true))
                return false;
        }

        return true;
    }

    private void GenerateFunction(CodeWriter w, FunctionItem func)
    {
        // Resolve return type
        var returnType = func.ReturnType != null
            ? _typeResolver.Resolve(func.ReturnType)
            : "void";

        // Build parameter list
        var parameters = new List<string>();
        foreach (var arg in func.Arguments)
        {
            if (arg.IsVarargs)
                continue;

            var paramName = TypeMapper.EscapeIdentifier(arg.Name ?? $"arg{parameters.Count}");

            // Marshal const char* parameters as string for convenient P/Invoke usage
            if (TypeResolver.IsConstCharPointer(arg.Type))
            {
                parameters.Add($"[MarshalAs(UnmanagedType.LPUTF8Str)] string {paramName}");
            }
            else
            {
                var paramType = arg.Type != null ? _typeResolver.Resolve(arg.Type) : "nint";
                parameters.Add($"{paramType} {paramName}");
            }
        }

        // Strip prefix from C# method name but keep original for EntryPoint
        var originalName = func.Name;
        var csMethodName = _config.StripFunctionPrefix(func.Name);

        // Write documentation
        w.WriteDocComment(
            func.Comments?.Preceding,
            func.Comments?.Attached);

        // Write the DllImport attribute with explicit EntryPoint
        w.WriteLine($"[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, EntryPoint = \"{originalName}\")]");

        // Build the method signature
        var paramList = string.Join(", ", parameters);
        w.WriteLine($"public static extern {returnType} {csMethodName}({paramList});");
    }
}

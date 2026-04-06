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

    public void Generate(CodeWriter w, List<FunctionItem> functions, string? className = null)
    {
        className ??= _config.PublicClassName;
        w.WriteLine($"public static unsafe partial class {className}");
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

        // Check if return type is a C bool that should be marshaled as C# bool
        var returnIsBool = TypeResolver.IsBoolType(func.ReturnType);
        if (returnIsBool)
            returnType = "bool";

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
            // Marshal C bool parameters as C# bool
            else if (TypeResolver.IsBoolType(arg.Type))
            {
                parameters.Add($"[MarshalAs(UnmanagedType.U1)] bool {paramName}");
            }
            // Marshal C bool* parameters as ref bool for convenient managed usage
            else if (TypeResolver.IsBoolPointer(arg.Type))
            {
                parameters.Add($"[MarshalAs(UnmanagedType.U1)] ref bool {paramName}");
            }
            // Marshal non-const pointers to blittable builtin types (int*, float*, double*, etc.) as ref T
            else if (TypeResolver.GetBlittableBuiltinPointerType(arg.Type) is { } refType)
            {
                parameters.Add($"ref {refType} {paramName}");
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

        // Add return marshaling attribute for bool return types
        if (returnIsBool)
            w.WriteLine("[return: MarshalAs(UnmanagedType.U1)]");

        // Build the method signature
        var paramList = string.Join(", ", parameters);
        w.WriteLine($"public static extern {returnType} {csMethodName}({paramList});");
    }
}

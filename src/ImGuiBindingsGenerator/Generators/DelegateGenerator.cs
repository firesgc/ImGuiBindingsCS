using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates C# delegate types from function pointer typedefs and struct fields.
/// </summary>
public sealed class DelegateGenerator
{
    private readonly GeneratorConfig _config;
    private readonly TypeResolver _typeResolver;
    private readonly ConditionalEvaluator _condEval;
    private readonly HashSet<string> _emittedNames;

    public DelegateGenerator(GeneratorConfig config, TypeResolver typeResolver, ConditionalEvaluator condEval, HashSet<string>? emittedNames = null)
    {
        _config = config;
        _typeResolver = typeResolver;
        _condEval = condEval;
        _emittedNames = emittedNames ?? [];
    }

    public void Generate(CodeWriter w, List<TypedefItem> typedefs, IReadOnlyList<DelegateInfo> additionalDelegates)
    {
        // Generate delegates from function pointer typedefs
        foreach (var typedef in typedefs)
        {
            if (!_condEval.ShouldInclude(typedef.Conditionals))
                continue;

            var delegateInfo = _typeResolver.ResolveFunctionPointerTypedef(typedef);
            if (delegateInfo == null)
                continue;

            // Skip already-emitted delegates (from other JSON files)
            if (!_emittedNames.Add(delegateInfo.Name))
                continue;

            GenerateDelegate(w, delegateInfo, typedef.Comments);
            w.WriteLine();
        }

        // Generate delegates discovered in struct fields
        foreach (var delegateInfo in additionalDelegates)
        {
            if (!_emittedNames.Add(delegateInfo.Name))
                continue;

            GenerateDelegate(w, delegateInfo, null);
            w.WriteLine();
        }
    }

    private void GenerateDelegate(CodeWriter w, DelegateInfo info, Comments? comments)
    {
        w.WriteDocComment(comments?.Preceding, comments?.Attached);

        bool hasPointers = info.Parameters.Any(p => TypeResolver.ContainsPointer(p.Type))
                        || TypeResolver.ContainsPointer(info.ReturnType);
        var unsafeModifier = hasPointers ? "unsafe " : "";

        w.WriteLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");

        // Add return marshaling attribute for bool return types
        if (info.ReturnType == "bool")
            w.WriteLine("[return: MarshalAs(UnmanagedType.U1)]");

        var paramList = string.Join(", ", info.Parameters.Select(p =>
            p.Type == "bool" ? $"[MarshalAs(UnmanagedType.U1)] bool {p.Name}" :
            p.Type == "ref bool" ? $"[MarshalAs(UnmanagedType.U1)] ref bool {p.Name}" :
            p.Type.StartsWith("ref ") ? $"{p.Type} {p.Name}" :
            $"{p.Type} {p.Name}"));
        w.WriteLine($"public {unsafeModifier}delegate {info.ReturnType} {info.Name}({paramList});");
    }
}

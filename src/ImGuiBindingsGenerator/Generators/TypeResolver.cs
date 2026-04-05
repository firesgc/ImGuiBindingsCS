using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Resolves TypeDescription from the JSON schema to C# type strings.
/// </summary>
public sealed class TypeResolver
{
    private readonly GeneratorConfig _config;
    private readonly HashSet<string> _knownEnumNames;
    private readonly HashSet<string> _knownStructNames;
    private readonly HashSet<string> _knownDelegateTypedefNames;
    private readonly Dictionary<string, string> _typeNameMap = new();

    public TypeResolver(GeneratorConfig config, NativeDefinitions defs)
    {
        _config = config;
        _knownEnumNames = defs.Enums.Select(e => e.Name).ToHashSet();
        _knownStructNames = defs.Structs.Select(s => s.Name).ToHashSet();

        // Track function pointer typedefs — these become C# delegates and make structs "managed"
        _knownDelegateTypedefNames = defs.Typedefs
            .Where(t => t.Type.TypeDetails?.Flavour == "function_pointer")
            .Select(t => t.Name)
            .ToHashSet();

        // Build mapping from original names to C# names
        foreach (var e in defs.Enums)
        {
            var csName = config.StripEnumTypePrefix(e.Name);
            _typeNameMap[e.Name] = csName;
            // Also map without trailing underscore
            _typeNameMap[e.Name.TrimEnd('_')] = csName;
        }
        foreach (var s in defs.Structs)
        {
            var csName = config.StripStructTypePrefix(s.Name);
            _typeNameMap[s.Name] = csName;
        }
    }

    /// <summary>
    /// Gets the C# name for a type, applying prefix stripping.
    /// </summary>
    public string GetCSharpTypeName(string originalName)
    {
        // Check typedef aliases first (ImU32 -> uint, etc.)
        if (TypeMapper.IsTypedefAlias(originalName))
            return TypeMapper.ResolveUserType(originalName);

        // Check our mapping
        if (_typeNameMap.TryGetValue(originalName, out var mapped))
            return mapped;

        return originalName;
    }

    /// <summary>
    /// Resolves a TypeDescription to a C# type string.
    /// </summary>
    public string Resolve(TypeDescription? desc)
    {
        if (desc == null)
            return "void";

        return desc.Kind switch
        {
            "Builtin" => TypeMapper.MapBuiltinType(desc.BuiltinType ?? "void"),
            "User" => GetCSharpTypeName(desc.Name ?? "void"),
            "Pointer" => ResolvePointer(desc),
            "Array" => ResolveArray(desc),
            "Type" => ResolveNamedType(desc),
            "Function" => "nint", // Function types become IntPtr in interop
            _ => "nint",
        };
    }

    /// <summary>
    /// Resolves a TypeInfo (declaration + description) to a C# type string.
    /// </summary>
    public string Resolve(TypeInfo? typeInfo)
    {
        if (typeInfo?.Description == null)
            return "void";

        return Resolve(typeInfo.Description);
    }

    private string ResolvePointer(TypeDescription desc)
    {
        var inner = desc.InnerType;
        if (inner == null)
            return "nint";

        // void* -> nint
        if (inner.Kind == "Builtin" && inner.BuiltinType == "void")
            return "nint";

        // const char* -> byte* (string)
        if (inner.Kind == "Builtin" && inner.BuiltinType == "char")
            return "byte*";

        // Function pointer -> nint
        if (inner.Kind == "Function")
            return "nint";

        // Pointer to pointer
        if (inner.Kind == "Pointer")
        {
            var innerResolved = ResolvePointer(inner);
            return $"{innerResolved}*";
        }

        var resolvedInner = Resolve(inner);
        return $"{resolvedInner}*";
    }

    private string ResolveArray(TypeDescription desc)
    {
        if (desc.InnerType == null)
            return "nint";

        var inner = Resolve(desc.InnerType);
        return $"{inner}*";
    }

    private string ResolveNamedType(TypeDescription desc)
    {
        // Type kind is used for function pointer parameters
        if (desc.InnerType?.Kind == "Pointer" && desc.InnerType.InnerType?.Kind == "Function")
            return "nint"; // Function pointers become IntPtr

        if (desc.InnerType != null)
            return Resolve(desc.InnerType);

        return desc.Name != null ? GetCSharpTypeName(desc.Name) : "nint";
    }

    /// <summary>
    /// Checks if a type name is a function pointer typedef (which becomes a C# delegate).
    /// Struct fields of delegate types make the struct "managed", causing CS8500 warnings
    /// when the struct is used as a pointer type. Such fields should be emitted as nint.
    /// </summary>
    public bool IsDelegateTypedef(string originalName) => _knownDelegateTypedefNames.Contains(originalName);

    /// <summary>
    /// Checks if a TypeDescription represents a plain C bool (not a pointer to bool).
    /// Used to marshal bool return types and parameters in function/delegate signatures.
    /// Struct fields and bool* pointers should remain as byte/byte*.
    /// </summary>
    public static bool IsBoolType(TypeDescription? desc)
    {
        return desc is { Kind: "Builtin", BuiltinType: "bool" };
    }

    /// <summary>
    /// Checks if a TypeInfo represents a plain C bool.
    /// </summary>
    public static bool IsBoolType(TypeInfo? typeInfo)
    {
        return IsBoolType(typeInfo?.Description);
    }

    /// <summary>
    /// Checks if a TypeDescription represents a const char* (read-only C string).
    /// This is: Pointer -> Builtin(char) with storage_classes containing "const".
    /// Used to distinguish read-only string parameters (which can be marshaled as string)
    /// from mutable char* buffers (which must remain byte*).
    /// </summary>
    public static bool IsConstCharPointer(TypeDescription? desc)
    {
        if (desc == null || desc.Kind != "Pointer")
            return false;

        var inner = desc.InnerType;
        return inner is { Kind: "Builtin", BuiltinType: "char" }
            && inner.StorageClasses?.Contains("const") == true;
    }

    /// <summary>
    /// Checks if a TypeInfo represents a const char*.
    /// </summary>
    public static bool IsConstCharPointer(TypeInfo? typeInfo)
    {
        return IsConstCharPointer(typeInfo?.Description);
    }

    /// <summary>
    /// Checks if a resolved type contains a pointer.
    /// </summary>
    public static bool ContainsPointer(string csType)
    {
        return csType.Contains('*') || csType == "nint" || csType == "nuint";
    }

    /// <summary>
    /// Resolves a function pointer typedef to a delegate signature.
    /// </summary>
    public DelegateInfo? ResolveFunctionPointerTypedef(TypedefItem typedef)
    {
        var typeDetails = typedef.Type.TypeDetails;
        if (typeDetails?.Flavour != "function_pointer")
            return null;

        var returnType = typeDetails.ReturnType != null ? Resolve(typeDetails.ReturnType) : "void";
        if (IsBoolType(typeDetails.ReturnType))
            returnType = "bool";
        var parameters = new List<(string Type, string Name)>();

        if (typeDetails.Arguments != null)
        {
            foreach (var arg in typeDetails.Arguments)
            {
                if (arg.IsVarargs) continue;
                var paramType = arg.Type != null ? Resolve(arg.Type) : "nint";
                if (IsBoolType(arg.Type?.Description))
                    paramType = "bool";
                var paramName = TypeMapper.EscapeIdentifier(arg.Name ?? $"arg{parameters.Count}");
                parameters.Add((paramType, paramName));
            }
        }

        return new DelegateInfo(typedef.Name, returnType, parameters);
    }

    /// <summary>
    /// Resolves a function pointer from a struct field's type_details.
    /// </summary>
    public DelegateInfo? ResolveFieldFunctionPointer(StructField field)
    {
        var typeDetails = field.Type.TypeDetails;
        if (typeDetails?.Flavour != "function_pointer")
            return null;

        var returnType = typeDetails.ReturnType != null ? Resolve(typeDetails.ReturnType) : "void";
        if (IsBoolType(typeDetails.ReturnType))
            returnType = "bool";
        var parameters = new List<(string Type, string Name)>();

        if (typeDetails.Arguments != null)
        {
            foreach (var arg in typeDetails.Arguments)
            {
                if (arg.IsVarargs) continue;
                var paramType = arg.Type != null ? Resolve(arg.Type) : "nint";
                if (IsBoolType(arg.Type?.Description))
                    paramType = "bool";
                var paramName = TypeMapper.EscapeIdentifier(arg.Name ?? $"arg{parameters.Count}");
                parameters.Add((paramType, paramName));
            }
        }

        // Generate a delegate name from the struct + field name
        var delegateName = $"{field.Name}_delegate";
        return new DelegateInfo(delegateName, returnType, parameters);
    }
}

/// <summary>
/// Represents a delegate type to be generated.
/// </summary>
public sealed record DelegateInfo(
    string Name,
    string ReturnType,
    List<(string Type, string Name)> Parameters);

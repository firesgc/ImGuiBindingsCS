namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Maps C/C++ types from dear_bindings JSON to C# types for P/Invoke interop.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    /// Known C builtin type to C# type mappings.
    /// </summary>
    private static readonly Dictionary<string, string> BuiltinTypeMap = new()
    {
        ["void"] = "void",
        ["bool"] = "byte",              // C bool is 1 byte, C# bool is not blittable
        ["char"] = "byte",              // C char = 1 byte
        ["unsigned_char"] = "byte",
        ["short"] = "short",
        ["unsigned_short"] = "ushort",
        ["int"] = "int",
        ["unsigned_int"] = "uint",
        ["long"] = "long",
        ["unsigned_long"] = "ulong",
        ["long_long"] = "long",
        ["unsigned_long_long"] = "ulong",
        ["float"] = "float",
        ["double"] = "double",
        ["size_t"] = "nuint",
    };

    /// <summary>
    /// Known ImGui typedef aliases that map directly to C# primitive types.
    /// </summary>
    private static readonly Dictionary<string, string> TypedefAliases = new()
    {
        ["ImS8"] = "sbyte",
        ["ImU8"] = "byte",
        ["ImS16"] = "short",
        ["ImU16"] = "ushort",
        ["ImS32"] = "int",
        ["ImU32"] = "uint",
        ["ImS64"] = "long",
        ["ImU64"] = "ulong",
        ["ImWchar16"] = "ushort",
        ["ImWchar32"] = "uint",
        ["ImGuiID"] = "uint",
        ["ImGuiKeyChord"] = "int",
        ["ImDrawIdx"] = "ushort",
        ["ImTextureID"] = "nint",
        ["ImFileHandle"] = "nint",
        ["size_t"] = "nuint",
        ["ImFontAtlasCustomRect"] = "ImFontAtlasRect", // typedef alias in JSON
    };

    /// <summary>
    /// C# reserved keywords that need to be escaped with @.
    /// </summary>
    private static readonly HashSet<string> CSharpKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    ];

    /// <summary>
    /// Fixed-size buffer compatible types for use with the C# fixed keyword.
    /// </summary>
    private static readonly HashSet<string> FixedBufferTypes =
    [
        "bool", "byte", "char", "short", "int", "long", "sbyte", "ushort", "uint", "ulong", "float", "double",
    ];

    /// <summary>
    /// Maps a builtin_type string from JSON to a C# type name.
    /// </summary>
    public static string MapBuiltinType(string builtinType)
    {
        return BuiltinTypeMap.TryGetValue(builtinType, out var csType) ? csType : builtinType;
    }

    /// <summary>
    /// Resolves a User type name (e.g., ImGuiID) to a C# type, applying typedef aliases.
    /// </summary>
    public static string ResolveUserType(string typeName)
    {
        return TypedefAliases.TryGetValue(typeName, out var csType) ? csType : typeName;
    }

    /// <summary>
    /// Checks if a user type is a known typedef alias to a primitive.
    /// </summary>
    public static bool IsTypedefAlias(string typeName)
    {
        return TypedefAliases.ContainsKey(typeName);
    }

    /// <summary>
    /// Escapes a C# keyword by prefixing with @.
    /// </summary>
    public static string EscapeIdentifier(string name)
    {
        return CSharpKeywords.Contains(name) ? $"@{name}" : name;
    }

    /// <summary>
    /// Determines if a C# type name can be used with the fixed keyword for arrays.
    /// </summary>
    public static bool IsFixedBufferCompatible(string csType)
    {
        return FixedBufferTypes.Contains(csType);
    }

    /// <summary>
    /// Register additional typedef aliases discovered from the JSON.
    /// </summary>
    public static void RegisterTypedefAlias(string name, string csType)
    {
        TypedefAliases.TryAdd(name, csType);
    }

    /// <summary>
    /// Gets the complete typedef alias dictionary for reference.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetTypedefAliases() => TypedefAliases;
}

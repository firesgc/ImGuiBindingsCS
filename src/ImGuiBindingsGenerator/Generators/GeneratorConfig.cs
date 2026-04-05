namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Configuration for the C# binding generator.
/// </summary>
public sealed class GeneratorConfig
{
    /// <summary>
    /// The C# namespace for generated bindings (default: "ImGui").
    /// </summary>
    public string Namespace { get; set; } = "ImGui";

    /// <summary>
    /// The native library name used in [DllImport] (default: "dcimgui").
    /// </summary>
    public string NativeLibraryName { get; set; } = "dcimgui";

    /// <summary>
    /// Prefixes to strip from function names (default: ["ImGui_"]).
    /// </summary>
    public List<string> FunctionPrefixesToStrip { get; set; } = ["ImGui_"];

    /// <summary>
    /// Prefixes to strip from enum type names (default: ["ImGui"]).
    /// The trailing '_' on enum type names is always stripped.
    /// </summary>
    public List<string> EnumTypePrefixesToStrip { get; set; } = ["ImGui"];

    /// <summary>
    /// Prefixes to strip from struct type names (default: ["ImGui"]).
    /// Types with only the "Im" prefix (e.g., ImVec2, ImDrawList) are kept as-is.
    /// </summary>
    public List<string> StructTypePrefixesToStrip { get; set; } = ["ImGui"];

    /// <summary>
    /// Whether to include internal definitions.
    /// </summary>
    public bool IncludeInternal { get; set; } = true;

    /// <summary>
    /// Whether to skip varargs functions.
    /// </summary>
    public bool SkipVarargsFunctions { get; set; } = true;

    /// <summary>
    /// Whether to skip ImStr helper functions.
    /// </summary>
    public bool SkipImStrHelpers { get; set; } = true;

    /// <summary>
    /// Known preprocessor defines and their evaluated state.
    /// </summary>
    public Dictionary<string, bool> KnownDefines { get; set; } = new()
    {
        ["IMGUI_DISABLE_OBSOLETE_FUNCTIONS"] = true,
        ["IMGUI_DISABLE_OBSOLETE_KEYIO"] = true,
        ["IMGUI_HAS_DOCK"] = true,
    };

    /// <summary>
    /// Output directory for generated files.
    /// </summary>
    public string OutputDirectory { get; set; } = "generated";

    /// <summary>
    /// Strips known prefixes from a function name.
    /// </summary>
    public string StripFunctionPrefix(string name)
    {
        foreach (var prefix in FunctionPrefixesToStrip)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return name[prefix.Length..];
        }
        return name;
    }

    /// <summary>
    /// Strips known prefixes from an enum type name and removes trailing underscore.
    /// </summary>
    public string StripEnumTypePrefix(string name)
    {
        // Remove trailing underscore first (e.g., ImGuiWindowFlags_ -> ImGuiWindowFlags)
        var cleaned = name.TrimEnd('_');

        foreach (var prefix in EnumTypePrefixesToStrip)
        {
            if (cleaned.StartsWith(prefix, StringComparison.Ordinal))
                return cleaned[prefix.Length..];
        }
        return cleaned;
    }

    /// <summary>
    /// Strips the enum type prefix from an enum element name.
    /// E.g., "ImGuiWindowFlags_NoTitleBar" with enum "ImGuiWindowFlags_" -> "NoTitleBar"
    /// </summary>
    public string StripEnumElementPrefix(string elementName, string enumName)
    {
        // The enum name includes trailing _, and element names use it as prefix
        if (elementName.StartsWith(enumName, StringComparison.Ordinal))
            return elementName[enumName.Length..];

        // Try without trailing _
        var enumPrefix = enumName.TrimEnd('_') + "_";
        if (elementName.StartsWith(enumPrefix, StringComparison.Ordinal))
            return elementName[enumPrefix.Length..];

        return elementName;
    }

    /// <summary>
    /// Strips known prefixes from a struct type name.
    /// </summary>
    public string StripStructTypePrefix(string name)
    {
        foreach (var prefix in StructTypePrefixesToStrip)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return name[prefix.Length..];
        }
        return name;
    }

    /// <summary>
    /// Maps an original C type name to a stripped C# type name.
    /// This handles enums, structs, and typedefs.
    /// </summary>
    public string MapTypeName(string originalName)
    {
        // Don't strip prefix from primitive typedef aliases like ImU32, ImS8, etc.
        if (TypeMapper.IsTypedefAlias(originalName))
            return TypeMapper.ResolveUserType(originalName);

        // Try stripping enum prefix (with trailing _)
        if (originalName.EndsWith('_'))
            return StripEnumTypePrefix(originalName);

        // Try stripping ImGui prefix for structs
        foreach (var prefix in StructTypePrefixesToStrip)
        {
            if (originalName.StartsWith(prefix, StringComparison.Ordinal))
                return originalName[prefix.Length..];
        }

        return originalName;
    }
}

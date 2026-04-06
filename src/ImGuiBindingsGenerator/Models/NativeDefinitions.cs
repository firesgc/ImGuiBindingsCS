using System.Text.Json.Serialization;

namespace ImGuiBindingsGenerator.Models;

/// <summary>
/// Root object representing the full JSON definition file from dear_bindings.
/// </summary>
public sealed class NativeDefinitions
{
    [JsonPropertyName("defines")]
    public List<DefineItem> Defines { get; set; } = [];

    [JsonPropertyName("enums")]
    public List<EnumItem> Enums { get; set; } = [];

    [JsonPropertyName("typedefs")]
    public List<TypedefItem> Typedefs { get; set; } = [];

    [JsonPropertyName("structs")]
    public List<StructItem> Structs { get; set; } = [];

    [JsonPropertyName("functions")]
    public List<FunctionItem> Functions { get; set; } = [];
}

// ── Defines ──────────────────────────────────────────────────────────

public sealed class DefineItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

// ── Enums ────────────────────────────────────────────────────────────

public sealed class EnumItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original_fully_qualified_name")]
    public string? OriginalFullyQualifiedName { get; set; }

    [JsonPropertyName("is_flags_enum")]
    public bool IsFlagsEnum { get; set; }

    [JsonPropertyName("elements")]
    public List<EnumElement> Elements { get; set; } = [];

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

public sealed class EnumElement
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value_expression")]
    public string? ValueExpression { get; set; }

    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("is_count")]
    public bool IsCount { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

// ── Typedefs ─────────────────────────────────────────────────────────

public sealed class TypedefItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public TypeInfo Type { get; set; } = new();

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

// ── Structs ──────────────────────────────────────────────────────────

public sealed class StructItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original_fully_qualified_name")]
    public string? OriginalFullyQualifiedName { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("by_value")]
    public bool ByValue { get; set; }

    [JsonPropertyName("forward_declaration")]
    public bool ForwardDeclaration { get; set; }

    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; set; }

    [JsonPropertyName("fields")]
    public List<StructField> Fields { get; set; } = [];

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

public sealed class StructField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("is_array")]
    public bool IsArray { get; set; }

    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; set; }

    [JsonPropertyName("array_bounds")]
    public string? ArrayBounds { get; set; }

    [JsonPropertyName("type")]
    public TypeInfo Type { get; set; } = new();

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

// ── Functions ────────────────────────────────────────────────────────

public sealed class FunctionItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original_fully_qualified_name")]
    public string? OriginalFullyQualifiedName { get; set; }

    [JsonPropertyName("return_type")]
    public TypeInfo? ReturnType { get; set; }

    [JsonPropertyName("arguments")]
    public List<FunctionArgument> Arguments { get; set; } = [];

    [JsonPropertyName("is_default_argument_helper")]
    public bool IsDefaultArgumentHelper { get; set; }

    [JsonPropertyName("is_manual_helper")]
    public bool IsManualHelper { get; set; }

    [JsonPropertyName("is_imstr_helper")]
    public bool IsImstrHelper { get; set; }

    [JsonPropertyName("has_imstr_helper")]
    public bool HasImstrHelper { get; set; }

    [JsonPropertyName("is_unformatted_helper")]
    public bool IsUnformattedHelper { get; set; }

    [JsonPropertyName("is_static")]
    public bool IsStatic { get; set; }

    [JsonPropertyName("original_class")]
    public string? OriginalClass { get; set; }

    [JsonPropertyName("comments")]
    public Comments? Comments { get; set; }

    [JsonPropertyName("is_internal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("conditionals")]
    public List<ConditionalItem>? Conditionals { get; set; }

    [JsonPropertyName("source_location")]
    public SourceLocation? SourceLocation { get; set; }
}

public sealed class FunctionArgument
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public TypeInfo? Type { get; set; }

    [JsonPropertyName("is_array")]
    public bool IsArray { get; set; }

    [JsonPropertyName("is_varargs")]
    public bool IsVarargs { get; set; }

    [JsonPropertyName("default_value")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("is_instance_pointer")]
    public bool IsInstancePointer { get; set; }

    [JsonPropertyName("array_bounds")]
    public string? ArrayBounds { get; set; }
}

// ── Type System ──────────────────────────────────────────────────────

public sealed class TypeInfo
{
    [JsonPropertyName("declaration")]
    public string Declaration { get; set; } = "";

    [JsonPropertyName("description")]
    public TypeDescription? Description { get; set; }

    [JsonPropertyName("type_details")]
    public TypeDetails? TypeDetails { get; set; }
}

public sealed class TypeDescription
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("builtin_type")]
    public string? BuiltinType { get; set; }

    [JsonPropertyName("inner_type")]
    public TypeDescription? InnerType { get; set; }

    [JsonPropertyName("is_nullable")]
    public bool? IsNullable { get; set; }

    [JsonPropertyName("storage_classes")]
    public List<string>? StorageClasses { get; set; }

    // For Function kind
    [JsonPropertyName("return_type")]
    public TypeDescription? ReturnType { get; set; }

    [JsonPropertyName("parameters")]
    public List<TypeDescription>? Parameters { get; set; }
}

/// <summary>
/// Additional type details for function pointers.
/// </summary>
public sealed class TypeDetails
{
    [JsonPropertyName("flavour")]
    public string? Flavour { get; set; }

    [JsonPropertyName("return_type")]
    public TypeInfo? ReturnType { get; set; }

    [JsonPropertyName("arguments")]
    public List<FunctionArgument>? Arguments { get; set; }
}

// ── Shared ───────────────────────────────────────────────────────────

public sealed class Comments
{
    [JsonPropertyName("preceding")]
    public List<string>? Preceding { get; set; }

    [JsonPropertyName("attached")]
    public string? Attached { get; set; }
}

public sealed class ConditionalItem
{
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = "";

    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";
}

public sealed class SourceLocation
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }
}

using System.Text.Json;
using ImGuiBindingsGenerator.Generators;
using ImGuiBindingsGenerator.Models;

// ── Parse command-line arguments ─────────────────────────────────────

var config = new GeneratorConfig();
string? inputDir = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--namespace" or "-n" when i + 1 < args.Length:
            config.Namespace = args[++i];
            break;
        case "--lib" or "-l" when i + 1 < args.Length:
            config.NativeLibraryName = args[++i];
            break;
        case "--output" or "-o" when i + 1 < args.Length:
            config.OutputDirectory = args[++i];
            break;
        case "--input" or "-i" when i + 1 < args.Length:
            inputDir = args[++i];
            break;
        case "--no-internal":
            config.IncludeInternal = false;
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            if (!args[i].StartsWith('-') && inputDir == null)
                inputDir = args[i];
            break;
    }
}

if (inputDir == null)
{
    Console.Error.WriteLine("Error: No input directory specified.");
    Console.Error.WriteLine("Usage: ImGuiBindingsGenerator <input-dir> [options]");
    Console.Error.WriteLine("Run with --help for more information.");
    return 1;
}

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Error: Input directory '{inputDir}' does not exist.");
    return 1;
}

// ── Discover JSON files ──────────────────────────────────────────────

var jsonFiles = Directory.GetFiles(inputDir, "*.json")
    .OrderBy(f => f)
    .ToList();

if (jsonFiles.Count == 0)
{
    Console.Error.WriteLine($"Error: No JSON files found in '{inputDir}'.");
    return 1;
}

Console.WriteLine($"Found {jsonFiles.Count} JSON definition file(s):");
foreach (var file in jsonFiles)
    Console.WriteLine($"  {Path.GetFileName(file)}");

// ── Delete output directory for a clean build ────────────────────────

if (Directory.Exists(config.OutputDirectory))
{
    Console.WriteLine();
    Console.WriteLine($"Cleaning output directory '{config.OutputDirectory}'...");
    Directory.Delete(config.OutputDirectory, recursive: true);
}

Directory.CreateDirectory(config.OutputDirectory);

// ── Parse all JSON files first (for cross-file type resolution) ──────

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
};

var allDefs = new List<(string BaseName, string JsonFile, NativeDefinitions Defs)>();
var condEval = new ConditionalEvaluator(config.KnownDefines);

foreach (var jsonFile in jsonFiles)
{
    var baseName = Path.GetFileNameWithoutExtension(jsonFile);
    Console.WriteLine();
    Console.WriteLine($"Parsing {baseName}...");

    try
    {
        var jsonText = File.ReadAllText(jsonFile);
        var defs = JsonSerializer.Deserialize<NativeDefinitions>(jsonText, jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {jsonFile}");
        allDefs.Add((baseName, jsonFile, defs));

        Console.WriteLine($"  Defines: {defs.Defines.Count}");
        Console.WriteLine($"  Enums: {defs.Enums.Count}");
        Console.WriteLine($"  Typedefs: {defs.Typedefs.Count}");
        Console.WriteLine($"  Structs: {defs.Structs.Count}");
        Console.WriteLine($"  Functions: {defs.Functions.Count}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  Error parsing {jsonFile}: {ex.Message}");
    }
}

// ── Register typedefs and build a combined type name map ─────────────

foreach (var (_, _, defs) in allDefs)
    RegisterTypedefs(defs, config);

// Build a combined TypeResolver that knows about ALL types across all JSONs
var combinedDefs = new NativeDefinitions
{
    Enums = allDefs.SelectMany(d => d.Defs.Enums).ToList(),
    Structs = allDefs.SelectMany(d => d.Defs.Structs).ToList(),
    Typedefs = allDefs.SelectMany(d => d.Defs.Typedefs).ToList(),
};

// ── Group JSON files into compatible sets ────────────────────────────
// Files with "nodefaultargfunctions" in the name are the raw API (no convenience
// overloads). The other files include default-argument convenience overloads.
// Including both sets together causes CS0111 (duplicate members), so we output
// each set into its own subdirectory.

const string NoDefaultsMarker = "nodefaultargfunctions";

var withDefaultsGroup = allDefs
    .Where(d => !d.BaseName.Contains(NoDefaultsMarker, StringComparison.OrdinalIgnoreCase))
    .ToList();

var withoutDefaultsGroup = allDefs
    .Where(d => d.BaseName.Contains(NoDefaultsMarker, StringComparison.OrdinalIgnoreCase))
    .ToList();

var groups = new List<(string SubDir, List<(string BaseName, string JsonFile, NativeDefinitions Defs)> Files)>();

if (withDefaultsGroup.Count > 0)
    groups.Add(("with_defaults", withDefaultsGroup));
if (withoutDefaultsGroup.Count > 0)
    groups.Add(("without_defaults", withoutDefaultsGroup));

// If only one group exists (e.g., user passed a directory with only one variant),
// output directly into the output directory without a subdirectory.
bool useSingleDir = groups.Count <= 1;

int totalFiles = 0;

foreach (var (subDir, groupFiles) in groups)
{
    var groupOutputDir = useSingleDir
        ? config.OutputDirectory
        : Path.Combine(config.OutputDirectory, subDir);

    Directory.CreateDirectory(groupOutputDir);

    Console.WriteLine();
    Console.WriteLine($"═══ Output: {(useSingleDir ? config.OutputDirectory : subDir + "/")} ═══");

    // ── Emit shared file (imgui_shared.cs) for this group ────────
    {
        var sw = new CodeWriter();
        sw.WriteLine("// Auto-generated by ImGuiBindingsGenerator");
        sw.WriteLine("// Shared definitions across all generated files");
        sw.WriteLine("using System;");
        sw.WriteLine("using System.Runtime.CompilerServices;");
        sw.WriteLine("using System.Runtime.InteropServices;");
        sw.WriteLine();
        sw.WriteLine($"namespace {config.Namespace};");
        sw.WriteLine();
        sw.WriteLine($"public static unsafe partial class {config.PublicClassName}");
        sw.OpenBrace();
        sw.WriteLine($"private const string LibName = \"{config.NativeLibraryName}\";");
        sw.CloseBrace();

        // Check if this group contains any internal files
        bool hasInternalFiles = groupFiles.Any(f =>
            f.BaseName.EndsWith("_internal", StringComparison.OrdinalIgnoreCase));

        if (hasInternalFiles)
        {
            sw.WriteLine();
            sw.WriteLine($"public static unsafe partial class {config.InternalClassName}");
            sw.OpenBrace();
            sw.WriteLine($"private const string LibName = \"{config.NativeLibraryName}\";");
            sw.CloseBrace();
        }

        var sharedPath = Path.Combine(groupOutputDir, "imgui_shared.cs");
        File.WriteAllText(sharedPath, sw.ToString());
        Console.WriteLine("  → imgui_shared.cs");
        totalFiles++;
    }

    // ── Process each JSON file (with per-group deduplication) ────
    var emittedConstants = new HashSet<string>();
    var emittedEnums = new HashSet<string>();
    var emittedStructs = new HashSet<string>();
    var emittedDelegates = new HashSet<string>();

    foreach (var (baseName, jsonFile, defs) in groupFiles)
    {
        Console.WriteLine();
        Console.WriteLine($"Generating {baseName}...");

        // Create TypeResolver with combined knowledge of all types
        var typeResolver = new TypeResolver(config, combinedDefs);

        // Single CodeWriter for the entire JSON → one .cs file
        var w = new CodeWriter();
        w.WriteLine("// Auto-generated by ImGuiBindingsGenerator");
        w.WriteLine("// Source: " + Path.GetFileName(jsonFile));
        w.WriteLine("using System;");
        w.WriteLine("using System.Runtime.CompilerServices;");
        w.WriteLine("using System.Runtime.InteropServices;");
        w.WriteLine();
        w.WriteLine($"namespace {config.Namespace};");
        w.WriteLine();

        // ── Constants ────────────────────────────────────────────
        var constantGen = new ConstantGenerator(config, condEval, emittedConstants);
        constantGen.Generate(w, defs.Defines);
        w.WriteLine();

        // ── Enums ────────────────────────────────────────────────
        var enumGen = new EnumGenerator(config, condEval, emittedEnums);
        enumGen.Generate(w, defs.Enums);

        // ── Structs ──────────────────────────────────────────────
        var structGen = new StructGenerator(config, typeResolver, condEval, emittedStructs);
        structGen.Generate(w, defs.Structs);

        // ── InlineArrays ─────────────────────────────────────────
        if (structGen.InlineArrays.Count > 0)
        {
            var inlineArrayGen = new InlineArrayGenerator(config);
            inlineArrayGen.Generate(w, structGen.InlineArrays);
        }

        // ── Delegates ────────────────────────────────────────────
        var delegateGen = new DelegateGenerator(config, typeResolver, condEval, emittedDelegates);
        delegateGen.Generate(w, defs.Typedefs, structGen.FieldDelegates);

        // ── Functions ────────────────────────────────────────────
        var functionGen = new FunctionGenerator(config, typeResolver, condEval);
        var isInternalFile = baseName.EndsWith("_internal", StringComparison.OrdinalIgnoreCase);
        var functionClassName = isInternalFile ? config.InternalClassName : config.PublicClassName;
        functionGen.Generate(w, defs.Functions, functionClassName);

        // Write the single output file
        var outputPath = Path.Combine(groupOutputDir, baseName + ".cs");
        File.WriteAllText(outputPath, w.ToString());
        Console.WriteLine($"  → {baseName}.cs");
        totalFiles++;
    }
}

Console.WriteLine();
Console.WriteLine($"Done! Generated {totalFiles} file(s) in '{config.OutputDirectory}'.");
return 0;

// ── Helper methods ───────────────────────────────────────────────────

static void RegisterTypedefs(NativeDefinitions defs, GeneratorConfig config)
{
    var condEval = new ConditionalEvaluator(config.KnownDefines);

    foreach (var typedef in defs.Typedefs)
    {
        // Respect conditional compilation
        if (!condEval.ShouldInclude(typedef.Conditionals))
            continue;

        var desc = typedef.Type.Description;
        if (desc == null) continue;

        // Map simple typedefs (e.g., ImWchar -> ImWchar32 -> uint)
        if (desc.Kind == "Builtin" && desc.BuiltinType != null)
        {
            var csType = TypeMapper.MapBuiltinType(desc.BuiltinType);
            TypeMapper.RegisterTypedefAlias(typedef.Name, csType);
        }
        else if (desc.Kind == "User" && desc.Name != null)
        {
            // Typedef to another typedef (e.g., ImWchar -> ImWchar32)
            if (TypeMapper.IsTypedefAlias(desc.Name))
            {
                TypeMapper.RegisterTypedefAlias(typedef.Name, TypeMapper.ResolveUserType(desc.Name));
            }
            else
            {
                // Typedef to a struct/other type (e.g., stbrp_node_im -> stbrp_node)
                // Register as an alias so the type resolver can find it
                TypeMapper.RegisterTypedefAlias(typedef.Name, desc.Name);
            }
        }
        else if (desc.Kind == "Pointer" && desc.InnerType != null)
        {
            // Pointer typedefs (e.g., ImBitArrayPtr -> ImU32* -> uint*)
            var innerType = ResolveTypedefPointerInner(desc.InnerType);
            if (innerType != null)
            {
                TypeMapper.RegisterTypedefAlias(typedef.Name, $"{innerType}*");
            }
        }
    }
}

static string? ResolveTypedefPointerInner(TypeDescription inner)
{
    if (inner.Kind == "Builtin" && inner.BuiltinType != null)
        return TypeMapper.MapBuiltinType(inner.BuiltinType);

    if (inner.Kind == "User" && inner.Name != null)
    {
        if (TypeMapper.IsTypedefAlias(inner.Name))
            return TypeMapper.ResolveUserType(inner.Name);
        return inner.Name;
    }

    if (inner.Kind == "Pointer" && inner.InnerType != null)
    {
        var resolved = ResolveTypedefPointerInner(inner.InnerType);
        return resolved != null ? $"{resolved}*" : null;
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("""
        ImGuiBindingsGenerator - Generate C# bindings from dear_bindings JSON definitions

        Usage: ImGuiBindingsGenerator <input-dir> [options]

        Arguments:
          <input-dir>         Directory containing JSON definition files

        Options:
          -n, --namespace <ns>    C# namespace for generated code (default: ImGui)
          -l, --lib <name>        Native library name for DllImport (default: jaose.engine)
          -o, --output <dir>      Output directory (default: generated)
          --no-internal           Exclude internal definitions
          -h, --help              Show this help message

        Examples:
          ImGuiBindingsGenerator ./DearBindings_docking
          ImGuiBindingsGenerator ./DearBindings_docking -n MyImGui -o ./output
          ImGuiBindingsGenerator ./DearBindings_docking --namespace ImGuiNative --lib cimgui
        """);
}

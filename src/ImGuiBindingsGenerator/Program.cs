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

// ── Process each JSON file ───────────────────────────────────────────

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
};

Directory.CreateDirectory(config.OutputDirectory);

int totalFiles = 0;

foreach (var jsonFile in jsonFiles)
{
    var baseName = Path.GetFileNameWithoutExtension(jsonFile);
    Console.WriteLine();
    Console.WriteLine($"Processing {baseName}...");

    NativeDefinitions defs;
    try
    {
        var jsonText = File.ReadAllText(jsonFile);
        defs = JsonSerializer.Deserialize<NativeDefinitions>(jsonText, jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {jsonFile}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  Error parsing {jsonFile}: {ex.Message}");
        continue;
    }

    Console.WriteLine($"  Defines: {defs.Defines.Count}");
    Console.WriteLine($"  Enums: {defs.Enums.Count}");
    Console.WriteLine($"  Typedefs: {defs.Typedefs.Count}");
    Console.WriteLine($"  Structs: {defs.Structs.Count}");
    Console.WriteLine($"  Functions: {defs.Functions.Count}");

    // Register typedef aliases found in JSON
    RegisterTypedefs(defs, config);

    // Create shared services
    var condEval = new ConditionalEvaluator(config.KnownDefines);
    var typeResolver = new TypeResolver(config, defs);

    // Determine output subdirectory based on the JSON file name
    var outputSubDir = Path.Combine(config.OutputDirectory, baseName);
    Directory.CreateDirectory(outputSubDir);

    // ── Generate Constants ───────────────────────────────────────
    var constantGen = new ConstantGenerator(config, condEval);
    var constantsCode = constantGen.Generate(defs.Defines);
    WriteFile(outputSubDir, "Constants.cs", constantsCode);

    // ── Generate Enums ───────────────────────────────────────────
    var enumGen = new EnumGenerator(config, condEval);
    var enumsCode = enumGen.Generate(defs.Enums);
    WriteFile(outputSubDir, "Enums.cs", enumsCode);

    // ── Generate Structs ─────────────────────────────────────────
    var structGen = new StructGenerator(config, typeResolver, condEval);
    var structsCode = structGen.Generate(defs.Structs);
    WriteFile(outputSubDir, "Structs.cs", structsCode);

    // ── Generate InlineArrays ────────────────────────────────────
    if (structGen.InlineArrays.Count > 0)
    {
        var inlineArrayGen = new InlineArrayGenerator(config);
        var inlineArraysCode = inlineArrayGen.Generate(structGen.InlineArrays);
        WriteFile(outputSubDir, "InlineArrays.cs", inlineArraysCode);
    }

    // ── Generate Delegates ───────────────────────────────────────
    var delegateGen = new DelegateGenerator(config, typeResolver, condEval);
    var delegatesCode = delegateGen.Generate(defs.Typedefs, structGen.FieldDelegates);
    WriteFile(outputSubDir, "Delegates.cs", delegatesCode);

    // ── Generate Functions ───────────────────────────────────────
    var functionGen = new FunctionGenerator(config, typeResolver, condEval);
    var functionsCode = functionGen.Generate(defs.Functions);
    WriteFile(outputSubDir, "Functions.cs", functionsCode);

    Console.WriteLine($"  Generated {6} files in {outputSubDir}");
    totalFiles += 6;
}

Console.WriteLine();
Console.WriteLine($"Done! Generated {totalFiles} files total in '{config.OutputDirectory}'.");
return 0;

// ── Helper methods ───────────────────────────────────────────────────

static void WriteFile(string dir, string fileName, string content)
{
    var path = Path.Combine(dir, fileName);
    File.WriteAllText(path, content);
    Console.WriteLine($"    → {fileName}");
}

static void RegisterTypedefs(NativeDefinitions defs, GeneratorConfig config)
{
    foreach (var typedef in defs.Typedefs)
    {
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
        }
    }
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
          -l, --lib <name>        Native library name for DllImport (default: dcimgui)
          -o, --output <dir>      Output directory (default: generated)
          --no-internal           Exclude internal definitions
          -h, --help              Show this help message

        Examples:
          ImGuiBindingsGenerator ./DearBindings_docking
          ImGuiBindingsGenerator ./DearBindings_docking -n MyImGui -o ./output
          ImGuiBindingsGenerator ./DearBindings_docking --namespace ImGuiNative --lib cimgui
        """);
}

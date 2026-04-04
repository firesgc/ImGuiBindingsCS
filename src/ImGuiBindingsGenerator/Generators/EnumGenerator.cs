using ImGuiBindingsGenerator.Models;

namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates C# enum types from ImGui enum definitions.
/// </summary>
public sealed class EnumGenerator
{
    private readonly GeneratorConfig _config;
    private readonly ConditionalEvaluator _condEval;

    public EnumGenerator(GeneratorConfig config, ConditionalEvaluator condEval)
    {
        _config = config;
        _condEval = condEval;
    }

    public void Generate(CodeWriter w, List<EnumItem> enums)
    {
        foreach (var enumDef in enums)
        {
            if (!_condEval.ShouldInclude(enumDef.Conditionals))
                continue;

            GenerateEnum(w, enumDef);
            w.WriteLine();
        }
    }

    private void GenerateEnum(CodeWriter w, EnumItem enumDef)
    {
        var csEnumName = _config.StripEnumTypePrefix(enumDef.Name);

        // Write documentation
        w.WriteDocComment(
            enumDef.Comments?.Preceding,
            enumDef.Comments?.Attached);

        // Write [Flags] attribute for bitmask enums
        if (enumDef.IsFlagsEnum)
            w.WriteLine("[Flags]");

        w.WriteLine($"public enum {csEnumName}");
        w.OpenBrace();

        // Use aligned block for enum members so trailing comments line up
        w.BeginAlignedBlock();

        foreach (var element in enumDef.Elements)
        {
            if (!_condEval.ShouldInclude(element.Conditionals))
                continue;

            // Skip COUNT entries (they are C-style sentinel values)
            if (element.IsCount)
                continue;

            var csElementName = _config.StripEnumElementPrefix(element.Name, enumDef.Name);

            // Ensure the element name is valid C# (can't start with a digit)
            if (csElementName.Length > 0 && char.IsDigit(csElementName[0]))
                csElementName = "_" + csElementName;

            // Escape keywords
            csElementName = TypeMapper.EscapeIdentifier(csElementName);

            var comment = CodeWriter.CleanTrailingComment(element.Comments?.Attached);
            w.WriteLineWithComment($"{csElementName} = {element.Value},", comment);
        }

        w.EndAlignedBlock();
        w.CloseBrace();
    }
}

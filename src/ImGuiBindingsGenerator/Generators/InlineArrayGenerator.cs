namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Generates [InlineArray] struct wrappers for non-primitive array fields.
/// </summary>
public sealed class InlineArrayGenerator
{
    private readonly GeneratorConfig _config;

    public InlineArrayGenerator(GeneratorConfig config)
    {
        _config = config;
    }

    public void Generate(CodeWriter w, IReadOnlyList<InlineArrayInfo> inlineArrays)
    {
        if (inlineArrays.Count == 0)
            return;

        foreach (var arr in inlineArrays)
        {
            if (arr.Size > 0)
            {
                w.WriteLine($"[InlineArray({arr.Size})]");
            }
            else if (arr.SizeExpression != null)
            {
                w.WriteLine($"// TODO: Size expression: {arr.SizeExpression}");
                w.WriteLine($"[InlineArray(1)] // Placeholder — resolve {arr.SizeExpression} manually");
            }

            w.WriteLine($"public struct {arr.Name}");
            w.OpenBrace();
            w.WriteLine($"public {arr.ElementType} Element;");
            w.CloseBrace();
            w.WriteLine();
        }
    }
}

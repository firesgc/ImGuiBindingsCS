namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Utility for writing formatted C# code with proper indentation.
/// </summary>
public sealed class CodeWriter
{
    private readonly StringWriter _writer = new();
    private int _indent;
    private bool _lineStart = true;

    public void WriteLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _writer.WriteLine();
            _lineStart = true;
            return;
        }

        WriteIndent();
        _writer.WriteLine(line);
        _lineStart = true;
    }

    public void Write(string text)
    {
        WriteIndent();
        _writer.Write(text);
        _lineStart = false;
    }

    public void OpenBrace()
    {
        WriteLine("{");
        _indent++;
    }

    public void CloseBrace(string suffix = "")
    {
        _indent--;
        WriteLine($"}}{suffix}");
    }

    public void Indent() => _indent++;
    public void Unindent() => _indent--;

    /// <summary>
    /// Writes a doc comment summary from preceding comments.
    /// </summary>
    public void WriteDocComment(IReadOnlyList<string>? precedingComments, string? attachedComment = null)
    {
        if (precedingComments != null && precedingComments.Count > 0)
        {
            WriteLine("/// <summary>");
            foreach (var line in precedingComments)
            {
                var cleaned = CleanComment(line);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    WriteLine($"/// {EscapeXml(cleaned)}");
            }
            WriteLine("/// </summary>");
        }
        else if (!string.IsNullOrEmpty(attachedComment))
        {
            var cleaned = CleanComment(attachedComment);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                WriteLine("/// <summary>");
                WriteLine($"/// {EscapeXml(cleaned)}");
                WriteLine("/// </summary>");
            }
        }
    }

    /// <summary>
    /// Writes an inline trailing comment.
    /// </summary>
    public string FormatTrailingComment(string? comment)
    {
        if (string.IsNullOrEmpty(comment))
            return "";

        var cleaned = CleanComment(comment);
        return string.IsNullOrWhiteSpace(cleaned) ? "" : $" // {cleaned}";
    }

    public override string ToString() => _writer.ToString();

    private void WriteIndent()
    {
        if (_lineStart)
        {
            for (int i = 0; i < _indent; i++)
                _writer.Write("    ");
            _lineStart = false;
        }
    }

    private static string CleanComment(string comment)
    {
        var trimmed = comment.Trim();
        // Strip leading // and trim
        if (trimmed.StartsWith("//"))
            trimmed = trimmed[2..].TrimStart();
        return trimmed;
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

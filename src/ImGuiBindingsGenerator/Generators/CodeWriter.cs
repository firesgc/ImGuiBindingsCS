namespace ImGuiBindingsGenerator.Generators;

/// <summary>
/// Utility for writing formatted C# code with proper indentation.
/// </summary>
public sealed class CodeWriter
{
    private readonly StringWriter _writer = new();
    private int _indent;
    private bool _lineStart = true;

    // Buffered lines for comment alignment within a block
    private List<(string Code, string? Comment)>? _alignBuffer;

    public void WriteLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            if (_alignBuffer != null)
            {
                _alignBuffer.Add(("", null));
            }
            else
            {
                _writer.WriteLine();
                _lineStart = true;
            }
            return;
        }

        if (_alignBuffer != null)
        {
            _alignBuffer.Add((GetIndentString() + line, null));
        }
        else
        {
            WriteIndent();
            _writer.WriteLine(line);
            _lineStart = true;
        }
    }

    /// <summary>
    /// Writes a line with a separate trailing comment, allowing alignment later.
    /// </summary>
    public void WriteLineWithComment(string code, string? comment)
    {
        if (_alignBuffer != null)
        {
            _alignBuffer.Add((GetIndentString() + code, comment));
        }
        else
        {
            // No alignment buffer, write immediately
            var trailing = FormatTrailingComment(comment);
            WriteIndent();
            _writer.WriteLine($"{code}{trailing}");
            _lineStart = true;
        }
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
    /// Begins buffering lines so trailing comments can be column-aligned.
    /// Call <see cref="EndAlignedBlock"/> to flush.
    /// </summary>
    public void BeginAlignedBlock()
    {
        _alignBuffer = [];
    }

    /// <summary>
    /// Flushes the buffered lines, aligning trailing comments to a common column.
    /// </summary>
    public void EndAlignedBlock()
    {
        if (_alignBuffer == null)
            return;

        // Find the maximum code width among lines that have comments
        int maxCodeWidth = 0;
        foreach (var (code, comment) in _alignBuffer)
        {
            if (!string.IsNullOrEmpty(comment) && !string.IsNullOrEmpty(code))
                maxCodeWidth = Math.Max(maxCodeWidth, code.Length);
        }

        // Write all buffered lines
        foreach (var (code, comment) in _alignBuffer)
        {
            if (string.IsNullOrEmpty(code))
            {
                _writer.WriteLine();
            }
            else if (!string.IsNullOrEmpty(comment))
            {
                _writer.Write(code);
                int padding = maxCodeWidth - code.Length + 1;
                if (padding < 1) padding = 1;
                _writer.Write(new string(' ', padding));
                _writer.WriteLine($"// {comment}");
            }
            else
            {
                _writer.WriteLine(code);
            }
        }

        _lineStart = true;
        _alignBuffer = null;
    }

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
    /// Cleans and returns a trailing comment string, or null if empty.
    /// </summary>
    public static string? CleanTrailingComment(string? comment)
    {
        if (string.IsNullOrEmpty(comment))
            return null;

        var cleaned = CleanComment(comment);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    /// <summary>
    /// Formats an inline trailing comment (legacy, for non-aligned use).
    /// </summary>
    public string FormatTrailingComment(string? comment)
    {
        if (string.IsNullOrEmpty(comment))
            return "";

        var cleaned = CleanComment(comment);
        return string.IsNullOrWhiteSpace(cleaned) ? "" : $" // {cleaned}";
    }

    public override string ToString() => _writer.ToString();

    private string GetIndentString()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _indent; i++)
            sb.Append("    ");
        return sb.ToString();
    }

    private void WriteIndent()
    {
        if (_lineStart)
        {
            for (int i = 0; i < _indent; i++)
                _writer.Write("    ");
            _lineStart = false;
        }
    }

    internal static string CleanComment(string comment)
    {
        var trimmed = comment.Trim();
        // Strip leading // and trim
        if (trimmed.StartsWith("//"))
            trimmed = trimmed[2..].TrimStart();
        return trimmed;
    }

    internal static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

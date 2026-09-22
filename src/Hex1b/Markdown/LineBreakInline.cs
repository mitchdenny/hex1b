namespace Hex1b.Markdown;

/// <summary>
/// A line break within inline content.
/// </summary>
public sealed class LineBreakInline : MarkdownInline
{
    /// <summary>
    /// Whether this is a hard break (two trailing spaces or backslash) vs. soft break (newline).
    /// </summary>
    public bool IsHard { get; }

    public LineBreakInline(bool isHard)
    {
        IsHard = isHard;
    }
}

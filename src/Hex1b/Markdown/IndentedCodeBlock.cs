namespace Hex1b.Markdown;

/// <summary>
/// An indented code block (4-space or 1-tab indent).
/// </summary>
public sealed class IndentedCodeBlock : MarkdownBlock
{
    /// <summary>
    /// The raw text content of the code block.
    /// </summary>
    public string Content { get; }

    public IndentedCodeBlock(string content)
    {
        Content = content;
    }
}

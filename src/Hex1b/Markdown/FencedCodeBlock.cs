namespace Hex1b.Markdown;

/// <summary>
/// A fenced code block (``` or ~~~).
/// </summary>
public sealed class FencedCodeBlock : MarkdownBlock
{
    /// <summary>
    /// The language identifier (e.g., "csharp", "json"), or empty string if none.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The raw text content of the code block.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The info string after the language (e.g., "title=example" in ```csharp title=example).
    /// </summary>
    public string InfoString { get; }

    public FencedCodeBlock(string language, string content, string infoString = "")
    {
        Language = language;
        Content = content;
        InfoString = infoString;
    }
}

namespace Hex1b.Markdown;

/// <summary>
/// Plain text content.
/// </summary>
public sealed class TextInline : MarkdownInline
{
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; }

    public TextInline(string text)
    {
        Text = text;
    }
}

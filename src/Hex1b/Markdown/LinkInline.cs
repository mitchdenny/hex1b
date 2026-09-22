namespace Hex1b.Markdown;

/// <summary>
/// An inline link (<c>[text](url)</c>).
/// </summary>
public sealed class LinkInline : MarkdownInline
{
    /// <summary>
    /// The link text (or child inlines if complex).
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The URL target.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Optional title attribute.
    /// </summary>
    public string? Title { get; }

    public LinkInline(string text, string url, string? title = null)
    {
        Text = text;
        Url = url;
        Title = title;
    }
}

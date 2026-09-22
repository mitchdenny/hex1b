namespace Hex1b.Markdown;

/// <summary>
/// An inline image (<c>![alt](url)</c>).
/// </summary>
public sealed class ImageInline : MarkdownInline
{
    /// <summary>
    /// The alt text.
    /// </summary>
    public string AltText { get; }

    /// <summary>
    /// The image URL.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Optional title attribute.
    /// </summary>
    public string? Title { get; }

    public ImageInline(string altText, string url, string? title = null)
    {
        AltText = altText;
        Url = url;
        Title = title;
    }
}

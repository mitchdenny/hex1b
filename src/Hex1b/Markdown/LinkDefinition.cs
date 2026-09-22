namespace Hex1b.Markdown;

/// <summary>
/// A reference link definition: [label]: url "optional title"
/// </summary>
public sealed class LinkDefinition
{
    public string Url { get; }
    public string? Title { get; }

    public LinkDefinition(string url, string? title = null)
    {
        Url = url;
        Title = title;
    }
}

namespace Hex1b.Markdown;

/// <summary>
/// An inline code span (`code`).
/// </summary>
public sealed class CodeInline : MarkdownInline
{
    /// <summary>
    /// The code text.
    /// </summary>
    public string Code { get; }

    public CodeInline(string code)
    {
        Code = code;
    }
}

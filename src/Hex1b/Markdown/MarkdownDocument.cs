namespace Hex1b.Markdown;

/// <summary>
/// Represents a parsed markdown document as a list of block-level elements.
/// </summary>
public sealed class MarkdownDocument
{
    /// <summary>
    /// The top-level blocks in the document.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; }

    public MarkdownDocument(IReadOnlyList<MarkdownBlock> blocks)
    {
        Blocks = blocks;
    }
}

/// <summary>
/// Base class for block-level markdown elements.
/// </summary>
public abstract class MarkdownBlock;

/// <summary>
/// A heading block (h1-h6).
/// </summary>
public sealed class HeadingBlock : MarkdownBlock
{
    /// <summary>
    /// The heading level (1-6).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// The inline content of the heading.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; }

    /// <summary>
    /// The plain text content of the heading (inlines flattened to text).
    /// </summary>
    public string Text { get; }

    public HeadingBlock(int level, IReadOnlyList<MarkdownInline> inlines, string text)
    {
        Level = level;
        Inlines = inlines;
        Text = text;
    }
}

/// <summary>
/// A paragraph block containing inline content.
/// </summary>
public sealed class ParagraphBlock : MarkdownBlock
{
    /// <summary>
    /// The inline content of the paragraph.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; }

    /// <summary>
    /// The plain text content (inlines flattened to text).
    /// </summary>
    public string Text { get; }

    public ParagraphBlock(IReadOnlyList<MarkdownInline> inlines, string text)
    {
        Inlines = inlines;
        Text = text;
    }
}

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

/// <summary>
/// A block quote (lines prefixed with &gt;).
/// </summary>
public sealed class BlockQuoteBlock : MarkdownBlock
{
    /// <summary>
    /// The nested blocks inside the block quote.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Children { get; }

    public BlockQuoteBlock(IReadOnlyList<MarkdownBlock> children)
    {
        Children = children;
    }
}

/// <summary>
/// A list block (ordered or unordered).
/// </summary>
public sealed class ListBlock : MarkdownBlock
{
    /// <summary>
    /// Whether this is an ordered list (1., 2., etc.) or unordered (-, *, +).
    /// </summary>
    public bool IsOrdered { get; }

    /// <summary>
    /// The starting number for ordered lists (usually 1).
    /// </summary>
    public int StartNumber { get; }

    /// <summary>
    /// The items in the list.
    /// </summary>
    public IReadOnlyList<ListItemBlock> Items { get; }

    public ListBlock(bool isOrdered, int startNumber, IReadOnlyList<ListItemBlock> items)
    {
        IsOrdered = isOrdered;
        StartNumber = startNumber;
        Items = items;
    }
}

/// <summary>
/// A single item within a list.
/// </summary>
public sealed class ListItemBlock : MarkdownBlock
{
    /// <summary>
    /// The nested blocks inside the list item.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Children { get; }

    /// <summary>
    /// Task list checkbox state. <c>null</c> for normal list items,
    /// <c>true</c> for checked (<c>[x]</c>), <c>false</c> for unchecked (<c>[ ]</c>).
    /// </summary>
    public bool? IsChecked { get; }

    public ListItemBlock(IReadOnlyList<MarkdownBlock> children, bool? isChecked = null)
    {
        Children = children;
        IsChecked = isChecked;
    }
}

/// <summary>
/// A thematic break (horizontal rule: ---, ***, ___).
/// </summary>
public sealed class ThematicBreakBlock : MarkdownBlock;

// --- Inline elements ---

/// <summary>
/// Base class for inline markdown elements (within paragraphs, headings, etc.).
/// </summary>
public abstract class MarkdownInline;

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

/// <summary>
/// Emphasis (bold or italic) wrapping inline content.
/// </summary>
public sealed class EmphasisInline : MarkdownInline
{
    /// <summary>
    /// Whether this is strong emphasis (bold) vs. regular emphasis (italic).
    /// </summary>
    public bool IsStrong { get; }

    /// <summary>
    /// The inline content within the emphasis.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Children { get; }

    public EmphasisInline(bool isStrong, IReadOnlyList<MarkdownInline> children)
    {
        IsStrong = isStrong;
        Children = children;
    }
}

/// <summary>
/// Strikethrough text (~~text~~).
/// </summary>
public sealed class StrikethroughInline : MarkdownInline
{
    /// <summary>
    /// The inline content within the strikethrough.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Children { get; }

    public StrikethroughInline(IReadOnlyList<MarkdownInline> children)
    {
        Children = children;
    }
}

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

/// <summary>
/// An inline link ([text](url)).
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

/// <summary>
/// An inline image (![alt](url)).
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

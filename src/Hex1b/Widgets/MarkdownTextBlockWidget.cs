using Hex1b.Markdown;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that renders inline markdown elements (bold, italic, code, links)
/// as styled, word-wrapped text. Used internally by the markdown renderer
/// for paragraphs, headings, and other inline-containing blocks.
/// </summary>
internal sealed record MarkdownTextBlockWidget(
    IReadOnlyList<MarkdownInline> Inlines) : Hex1bWidget
{
    /// <summary>
    /// Optional base foreground color applied to all text (e.g., heading color).
    /// </summary>
    internal Hex1bColor? BaseForeground { get; init; }

    /// <summary>
    /// Optional base attributes applied to all text (e.g., Bold for headings).
    /// </summary>
    internal CellAttributes BaseAttributes { get; init; }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MarkdownTextBlockNode ?? new MarkdownTextBlockNode();

        // Check if content changed
        if (!ReferenceEquals(node.Inlines, Inlines)
            || !ColorsEqual(node.BaseForeground, BaseForeground)
            || node.BaseAttributes != BaseAttributes)
        {
            node.MarkDirty();
        }

        node.Inlines = Inlines;
        node.BaseForeground = BaseForeground;
        node.BaseAttributes = BaseAttributes;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MarkdownTextBlockNode);

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Value.IsDefault == b.Value.IsDefault
            && a.Value.R == b.Value.R
            && a.Value.G == b.Value.G
            && a.Value.B == b.Value.B;
    }
}

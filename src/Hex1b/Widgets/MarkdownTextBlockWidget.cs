using Hex1b.Events;
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

    /// <summary>
    /// When <c>true</c>, links within the text become focusable nodes.
    /// </summary>
    internal bool FocusableLinks { get; init; }

    /// <summary>
    /// Handler invoked when a link is activated.
    /// </summary>
    internal Func<MarkdownLinkActivatedEventArgs, Task>? LinkActivatedHandler { get; init; }

    /// <summary>
    /// The source markdown widget (for building event args).
    /// </summary>
    internal MarkdownWidget? SourceWidget { get; init; }

    /// <summary>
    /// Number of columns to indent continuation lines (used for list items
    /// where the marker occupies the first N columns of line 1).
    /// </summary>
    internal int HangingIndent { get; init; }

    /// <summary>
    /// Optional prefix string to prepend on continuation lines instead of spaces.
    /// When set, continuation lines use this prefix (e.g., "│ " for block quotes)
    /// instead of <c>new string(' ', HangingIndent)</c>.
    /// </summary>
    internal string? ContinuationPrefix { get; init; }

    /// <summary>
    /// Optional anchor identifier for heading nodes, used for intra-document
    /// link navigation (e.g., "getting-started" for a "Getting Started" heading).
    /// </summary>
    internal string? AnchorId { get; init; }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MarkdownTextBlockNode ?? new MarkdownTextBlockNode();

        // Check if content changed
        if (!ReferenceEquals(node.Inlines, Inlines)
            || !ColorsEqual(node.BaseForeground, BaseForeground)
            || node.BaseAttributes != BaseAttributes
            || node.FocusableLinks != FocusableLinks
            || node.HangingIndent != HangingIndent
            || node.ContinuationPrefix != ContinuationPrefix)
        {
            node.MarkDirty();
        }

        node.Inlines = Inlines;
        node.BaseForeground = BaseForeground;
        node.BaseAttributes = BaseAttributes;
        node.FocusableLinks = FocusableLinks;
        node.LinkActivatedHandler = LinkActivatedHandler;
        node.SourceWidget = SourceWidget;
        node.HangingIndent = HangingIndent;
        node.ContinuationPrefix = ContinuationPrefix;
        node.AnchorId = AnchorId;

        // Create/update link region nodes during reconciliation
        // so they exist for the FocusRing before MeasureCore runs
        if (FocusableLinks)
        {
            node.ReconcileLinkRegions(Inlines);
        }
        else
        {
            node.ClearLinkRegions();
        }

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

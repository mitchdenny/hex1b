using System.Collections.Immutable;
using Hex1b.Layout;
using Hex1b.Markdown;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="MarkdownWidget"/>. Parses markdown source into an AST,
/// builds a widget tree via <see cref="MarkdownWidgetRenderer"/>, and delegates
/// layout and rendering to the composed child node.
/// </summary>
public sealed class MarkdownNode : Hex1bNode
{
    private MarkdownDocument? _cachedDocument;
    private string? _lastParsedSource;

    /// <summary>
    /// The markdown source text.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// The block handler chain from the widget.
    /// </summary>
    public ImmutableList<(Type BlockType, Delegate Handler)> BlockHandlers { get; set; }
        = ImmutableList<(Type, Delegate)>.Empty;

    /// <summary>
    /// The reconciled child node (typically a VStackNode).
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }

    public override bool IsFocusable => false;

    /// <summary>
    /// Parse the source and build a widget tree for reconciliation.
    /// </summary>
    internal Hex1bWidget BuildWidgetTree()
    {
        // Re-parse only when source changes
        if (_cachedDocument == null || _lastParsedSource != Source)
        {
            _cachedDocument = MarkdownParser.Parse(Source);
            _lastParsedSource = Source;
        }

        return MarkdownWidgetRenderer.Render(_cachedDocument, BlockHandlers);
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (ContentChild != null)
        {
            return ContentChild.Measure(constraints);
        }

        return constraints.Constrain(new Size(0, 0));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        ContentChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        ContentChild?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ContentChild != null)
            yield return ContentChild;
    }
}

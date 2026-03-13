using System.Collections.Immutable;
using Hex1b.Documents;
using Hex1b.Events;
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
    /// The backing <see cref="IHex1bDocument"/>, if the widget was constructed from one.
    /// </summary>
    public IHex1bDocument? Document { get; set; }

    /// <summary>
    /// The <see cref="IHex1bDocument.Version"/> at the time the source was last extracted.
    /// Used for efficient change detection.
    /// </summary>
    public long DocumentVersion { get; set; }

    /// <summary>
    /// The block handler chain from the widget.
    /// </summary>
    public ImmutableList<(Type BlockType, Delegate Handler)> BlockHandlers { get; set; }
        = ImmutableList<(Type, Delegate)>.Empty;

    /// <summary>
    /// When <c>true</c>, links within the content become focusable nodes.
    /// </summary>
    public bool FocusableChildren { get; set; }

    /// <summary>
    /// Handler invoked when a link is activated.
    /// </summary>
    public Func<MarkdownLinkActivatedEventArgs, Task>? LinkActivatedHandler { get; set; }

    /// <summary>
    /// The source widget for building event args.
    /// </summary>
    public MarkdownWidget? SourceWidget { get; set; }

    /// <summary>
    /// Callback for loading and decoding images.
    /// </summary>
    public MarkdownImageLoader? ImageLoader { get; set; }

    /// <summary>
    /// The reconciled child node (typically a VStackNode).
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }

    /// <summary>
    /// Mapping of heading slug to the corresponding heading node, used for
    /// intra-document link navigation.
    /// </summary>
    internal Dictionary<string, Hex1bNode> HeadingAnchors { get; } = new(StringComparer.Ordinal);

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

        return MarkdownWidgetRenderer.Render(
            _cachedDocument, BlockHandlers, FocusableChildren, LinkActivatedHandler, SourceWidget, ImageLoader);
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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
                yield return focusable;
        }
    }

    /// <summary>
    /// Walks the child node tree and rebuilds the <see cref="HeadingAnchors"/> map
    /// from all <see cref="MarkdownTextBlockNode"/> nodes that have an
    /// <see cref="MarkdownTextBlockNode.AnchorId"/>.
    /// </summary>
    internal void RebuildHeadingAnchors()
    {
        HeadingAnchors.Clear();
        if (ContentChild != null)
        {
            CollectAnchors(ContentChild);
        }
    }

    private void CollectAnchors(Hex1bNode node)
    {
        if (node is MarkdownTextBlockNode textBlock && textBlock.AnchorId != null)
        {
            // First occurrence wins (matches GitHub behaviour for duplicate headings)
            HeadingAnchors.TryAdd(textBlock.AnchorId, textBlock);
        }

        foreach (var child in node.GetChildren())
        {
            CollectAnchors(child);
        }
    }
}

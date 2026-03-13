using System.Collections.Immutable;
using Hex1b.Documents;
using Hex1b.Events;
using Hex1b.Markdown;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that parses markdown source and renders it as a composed widget tree.
/// Block rendering is extensible via <see cref="OnBlock{TBlock}"/> which registers
/// middleware-style handler callbacks.
/// </summary>
/// <param name="Source">The markdown source text.</param>
public sealed record MarkdownWidget(string Source) : Hex1bWidget
{
    /// <summary>
    /// Creates a <see cref="MarkdownWidget"/> that reads its content from an
    /// <see cref="IHex1bDocument"/>. The document's <see cref="IHex1bDocument.Version"/>
    /// is used for efficient change detection; re-parsing only occurs when the
    /// version advances.
    /// </summary>
    /// <param name="document">The document to render as markdown.</param>
    public MarkdownWidget(IHex1bDocument document) : this(document.GetText())
    {
        Document = document;
    }

    /// <summary>
    /// The backing document, if constructed from an <see cref="IHex1bDocument"/>.
    /// When set, <see cref="IHex1bDocument.Version"/> is used for change detection
    /// instead of string equality.
    /// </summary>
    internal IHex1bDocument? Document { get; init; }

    /// <summary>
    /// Registered block handlers as (BlockType, Delegate) pairs.
    /// Last entry is called first; each can invoke <see cref="MarkdownBlockContext.Default"/>
    /// to chain to the next handler (or the built-in default).
    /// </summary>
    internal ImmutableList<(Type BlockType, Delegate Handler)> BlockHandlers { get; init; }
        = ImmutableList<(Type, Delegate)>.Empty;

    /// <summary>
    /// When <c>true</c>, links within the markdown content become focusable nodes
    /// that participate in Tab/Shift+Tab navigation.
    /// </summary>
    internal bool FocusableChildren { get; init; }

    /// <summary>
    /// Handler invoked when a link is activated (Enter key on focused link).
    /// Set <see cref="MarkdownLinkActivatedEventArgs.Handled"/> to suppress default behavior.
    /// </summary>
    internal Func<MarkdownLinkActivatedEventArgs, Task>? LinkActivatedHandler { get; init; }

    /// <summary>
    /// Callback for loading and decoding images referenced in markdown content.
    /// When set, image-only paragraphs are rendered as <see cref="Hex1b.Widgets.KgpImageWidget"/>
    /// instances. When null or when the callback returns null, images fall back to text rendering.
    /// </summary>
    internal MarkdownImageLoader? ImageLoader { get; init; }

    /// <summary>
    /// Registers a handler for a specific block type. Multiple handlers for the same
    /// type form a middleware chain: the last registered is called first. Call
    /// <see cref="MarkdownBlockContext.Default"/> within your handler to invoke the
    /// next handler in the chain (or the built-in default).
    /// </summary>
    /// <typeparam name="TBlock">The markdown block type to handle.</typeparam>
    /// <param name="handler">
    /// A function that receives the rendering context and the block, and returns a widget.
    /// Use <c>ctx.Default(block)</c> to delegate to the next handler.
    /// </param>
    public MarkdownWidget OnBlock<TBlock>(
        Func<MarkdownBlockContext, TBlock, Hex1bWidget> handler)
        where TBlock : MarkdownBlock
        => this with { BlockHandlers = BlockHandlers.Add((typeof(TBlock), handler)) };

    /// <summary>
    /// Enables or disables focusable children (links) in the markdown content.
    /// When enabled, links become Tab-focusable and the containing scroll panel
    /// auto-scrolls to show the focused link.
    /// </summary>
    /// <param name="children">Whether child links should be focusable.</param>
    public MarkdownWidget Focusable(bool children = false)
        => this with { FocusableChildren = children };

    /// <summary>
    /// Registers a synchronous handler for link activation events.
    /// </summary>
    public MarkdownWidget OnLinkActivated(Action<MarkdownLinkActivatedEventArgs> handler)
        => this with { LinkActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Registers an asynchronous handler for link activation events.
    /// </summary>
    public MarkdownWidget OnLinkActivated(Func<MarkdownLinkActivatedEventArgs, Task> handler)
        => this with { LinkActivatedHandler = handler };

    /// <summary>
    /// Registers an image loader callback for rendering embedded images.
    /// The callback receives a <see cref="Uri"/> (relative or absolute) and the alt text,
    /// and should return decoded RGBA pixel data, or <c>null</c> to fall back to text rendering.
    /// </summary>
    public MarkdownWidget OnImageLoad(MarkdownImageLoader loader)
        => this with { ImageLoader = loader };

    internal override async Task<Hex1bNode> ReconcileAsync(
        Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MarkdownNode ?? new MarkdownNode();

        if (Document != null)
        {
            // Document-backed: use version for efficient change detection
            var currentVersion = Document.Version;
            if (node.Document != Document || node.DocumentVersion != currentVersion)
            {
                node.Document = Document;
                node.DocumentVersion = currentVersion;
                node.Source = Document.GetText();
                node.MarkDirty();
            }
        }
        else
        {
            // String-backed: compare source text directly
            if (node.Source != Source)
            {
                node.Source = Source;
                node.Document = null;
                node.MarkDirty();
            }
        }

        node.BlockHandlers = BlockHandlers;
        node.FocusableChildren = FocusableChildren;
        node.LinkActivatedHandler = LinkActivatedHandler;
        node.ImageLoader = ImageLoader;
        node.SourceWidget = this;

        // Build the widget tree from parsed markdown
        var contentWidget = node.BuildWidgetTree();

        // Reconcile the content as a child
        node.ContentChild = await context.ReconcileChildAsync(
            node.ContentChild, contentWidget, node);

        // Rebuild heading anchor map for intra-document link navigation
        node.RebuildHeadingAnchors();

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(MarkdownNode);
}

using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A standalone scrollbar widget that can be composed with other widgets.
/// </summary>
/// <param name="Orientation">Whether the scrollbar is vertical or horizontal.</param>
/// <param name="ContentSize">The total size of the content being scrolled.</param>
/// <param name="ViewportSize">The visible viewport size.</param>
/// <param name="Offset">The current scroll offset.</param>
public sealed record ScrollbarWidget(
    ScrollOrientation Orientation,
    int ContentSize,
    int ViewportSize,
    int Offset) : Hex1bWidget
{
    /// <summary>
    /// Handler called when the scroll offset changes.
    /// </summary>
    internal Func<int, Task>? ScrollHandler { get; init; }

    /// <summary>
    /// Sets the handler for scroll offset changes.
    /// </summary>
    public ScrollbarWidget OnScroll(Action<int> handler)
        => this with { ScrollHandler = offset => { handler(offset); return Task.CompletedTask; } };

    /// <summary>
    /// Sets the async handler for scroll offset changes.
    /// </summary>
    public ScrollbarWidget OnScroll(Func<int, Task> handler)
        => this with { ScrollHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScrollbarNode ?? new ScrollbarNode();

        node.Orientation = Orientation;
        node.ContentSize = ContentSize;
        node.ViewportSize = ViewportSize;
        node.Offset = Offset;
        node.ScrollHandler = ScrollHandler;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ScrollbarNode);
}

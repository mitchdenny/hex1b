using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public abstract record Hex1bWidget
{
    /// <summary>
    /// Callback to configure input bindings for this widget.
    /// The callback receives a builder pre-populated with the widget's default bindings.
    /// </summary>
    internal Action<InputBindingsBuilder>? BindingsConfigurator { get; init; }

    /// <summary>
    /// Optional cache eligibility predicate for this widget's reconciled node.
    /// Returning <c>false</c> forces a cache miss for that subtree on the current frame.
    /// </summary>
    internal Func<Hex1bNode, bool>? CachePredicate { get; init; }

    /// <summary>
    /// Hint for how this widget should be sized horizontally within its parent.
    /// Used by HStack to distribute width among children.
    /// </summary>
    public SizeHint? WidthHint { get; init; }

    /// <summary>
    /// Hint for how this widget should be sized vertically within its parent.
    /// Used by VStack to distribute height among children.
    /// </summary>
    public SizeHint? HeightHint { get; init; }

    /// <summary>
    /// A user-assigned name for this widget used as a tag value in per-node metrics.
    /// When per-node metrics are enabled, this name becomes a segment in the hierarchical
    /// metric path (e.g., <c>root.sidebar.orders</c>). If null, an auto-generated name
    /// based on the node type and child index is used (e.g., <c>VStack[0]</c>).
    /// </summary>
    public string? MetricName { get; init; }

    /// <summary>
    /// Delay after which this widget requests a redraw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, a one-shot timer is scheduled during reconciliation that will
    /// trigger a re-render after the specified delay. Use this for animations.
    /// </para>
    /// <para>
    /// The delay is clamped to a minimum of 16ms (60 FPS cap).
    /// For continuous animation, call <c>.RedrawAfter()</c> on each build.
    /// </para>
    /// </remarks>
    public TimeSpan? RedrawDelay { get; init; }

    /// <summary>
    /// Gets the effective redraw delay for this widget.
    /// Override in derived classes to provide computed defaults.
    /// </summary>
    internal virtual TimeSpan? GetEffectiveRedrawDelay() => RedrawDelay;

    /// <summary>
    /// Creates or updates a node from this widget asynchronously.
    /// </summary>
    /// <param name="existingNode">The existing node to update, or null to create a new one.</param>
    /// <param name="context">The reconciliation context with helpers for child reconciliation and focus.</param>
    /// <returns>A task that resolves to the reconciled node.</returns>
    internal abstract Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context);

    /// <summary>
    /// Gets the expected node type for this widget. Used to determine if an existing node can be reused.
    /// </summary>
    internal abstract Type GetExpectedNodeType();
}

using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Base class for every Hex1b widget.
/// </summary>
/// <remarks>
/// <para>
/// There are two ways to author a widget:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Compositional</b> (the recommended path for most widgets): override
/// <see cref="Build(CompositionContext)"/> and return a tree of other widgets. The
/// framework handles reconciliation, layout, focus preservation, and ambient state for
/// you. Authors never need to write a custom <see cref="Hex1bNode"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Primitive</b> (used by the built-in widgets that touch the terminal directly):
/// pair the widget with a custom <see cref="Hex1bNode"/> that implements
/// <c>Render</c>/<c>Measure</c>/<c>Arrange</c>, and override
/// <c>ReconcileAsync</c>/<c>GetExpectedNodeType</c> to wire them together. Note that
/// these overrides are <c>internal</c>, so the primitive path is only available inside
/// the Hex1b assembly.
/// </description>
/// </item>
/// </list>
/// <para>
/// A widget should pick exactly one path. Overriding both <see cref="Build"/> and
/// <c>ReconcileAsync</c> is a programming error and is flagged by analyzer HEX1B0010.
/// At runtime, an explicit <c>ReconcileAsync</c> override always wins and any
/// <see cref="Build"/> implementation is ignored.
/// </para>
/// </remarks>
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
    internal Func<RenderCacheContext, bool>? CachePredicate { get; init; }

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
    /// Per-widget default for <see cref="WidthHint"/> when the user hasn't
    /// explicitly set one. Returning a non-null value lets a widget opt into
    /// "expand to fill" or any other sizing behavior without forcing every
    /// caller to chain <c>.FillWidth()</c>. The user-supplied
    /// <see cref="WidthHint"/> always wins over this default.
    /// </summary>
    protected internal virtual SizeHint? DefaultWidthHint => null;

    /// <summary>
    /// Per-widget default for <see cref="HeightHint"/>. See
    /// <see cref="DefaultWidthHint"/> for the rationale.
    /// </summary>
    protected internal virtual SizeHint? DefaultHeightHint => null;

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
    /// Builds the widget tree for this widget. Override this method to author a widget
    /// compositionally — by returning a tree of other widgets — without writing a custom
    /// <see cref="Hex1bNode"/>.
    /// </summary>
    /// <param name="ctx">
    /// The composition context. Exposes per-instance state via
    /// <see cref="CompositionContext.UseState{T}(System.Func{T})"/>, ambient values via
    /// <see cref="CompositionContext.Provide{T}(T)"/> and <see cref="CompositionContext.Use{T}"/>,
    /// and the full fluent widget-building API (<c>ctx.Text(...)</c>, <c>ctx.VStack(...)</c>,
    /// <c>ctx.Button(...)</c>, etc.).
    /// </param>
    /// <returns>
    /// The widget tree to render. Returns <c>null</c> by default, which signals to the
    /// framework that this widget does not use the compositional path. Widgets that
    /// override this method must return a non-null tree.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Build is invoked synchronously on every reconciliation pass. Heavy work belongs in
    /// state objects that are computed once and read on subsequent frames.
    /// </para>
    /// <para>
    /// A widget must override either <see cref="Build"/> or the primitive
    /// <c>ReconcileAsync</c>/<c>GetExpectedNodeType</c> pair, but never both. Overriding
    /// both is flagged by analyzer HEX1B0010; at runtime, an explicit
    /// <c>ReconcileAsync</c> override always wins.
    /// </para>
    /// </remarks>
    protected virtual Hex1bWidget? Build(CompositionContext ctx) => null;

    /// <summary>
    /// Creates or updates a node from this widget asynchronously.
    /// </summary>
    /// <param name="existingNode">The existing node to update, or null to create a new one.</param>
    /// <param name="context">The reconciliation context with helpers for child reconciliation and focus.</param>
    /// <returns>A task that resolves to the reconciled node.</returns>
    /// <remarks>
    /// The default implementation dispatches to <see cref="Build(CompositionContext)"/> via
    /// the standard composite reconciliation pipeline. Built-in primitive widgets override
    /// this method (and <see cref="GetExpectedNodeType"/>) to wire up a custom node type
    /// that paints directly to the terminal. The override is <c>internal</c>, so external
    /// widget authors should always use the compositional <see cref="Build"/> path.
    /// </remarks>
    internal virtual Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
        => CompositeReconciler.ReconcileAsync(this, existingNode, context);

    /// <summary>
    /// Gets the expected node type for this widget. Used to determine if an existing node can be reused.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>Hex1bCompositeNode</c>, which is the shared node type used by the
    /// compositional path. Primitive widgets that supply their own node must override this
    /// method (alongside <see cref="ReconcileAsync"/>) to return their concrete node type.
    /// </remarks>
    internal virtual Type GetExpectedNodeType() => typeof(Hex1bCompositeNode);

    /// <summary>
    /// Invokes the protected <see cref="Build(CompositionContext)"/> method. Used by
    /// <see cref="CompositeReconciler"/> to dispatch into widgets it doesn't have
    /// <c>protected</c> access to.
    /// </summary>
    internal Hex1bWidget? InvokeBuild(CompositionContext ctx) => Build(ctx);
}

namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Marker record for the root context (no parent widget constraint).
/// This widget should never be reconciled - it's purely a type marker.
/// </summary>
public sealed record RootWidget : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
        => throw new NotSupportedException("RootWidget is a type marker and should not be reconciled.");

    internal override Type GetExpectedNodeType()
        => throw new NotSupportedException("RootWidget is a type marker and should not be reconciled.");
}

/// <summary>
/// A context for building widgets within a parent container.
/// The TParentWidget type constrains which child widgets can be created.
/// Extension methods return widgets directly; covariance allows collection expressions.
/// </summary>
/// <typeparam name="TParentWidget">The parent widget type - constrains valid children.</typeparam>
public class WidgetContext<TParentWidget>
    where TParentWidget : Hex1bWidget
{
    internal WidgetContext() { }
}

/// <summary>
/// Root context for starting widget tree construction.
/// </summary>
public class RootContext : WidgetContext<RootWidget>
{
    /// <summary>
    /// The cancellation token for the application lifecycle.
    /// Use this to observe when the application is shutting down.
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}

using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Base class for composite widgets that build their UI from other widgets.
/// Composite widgets describe reusable UI patterns like Picker, DatePicker, ColorPicker, etc.
/// </summary>
/// <typeparam name="TNode">The node type that manages this composite widget's state.</typeparam>
/// <remarks>
/// <para>
/// CompositeWidget enables building complex widgets from simpler ones while maintaining
/// proper reconciliation semantics. The <see cref="BuildContentAsync"/> method returns
/// the widget tree that represents this composite's visual content.
/// </para>
/// <para>
/// The composite node holds state that survives reconciliation (e.g., popup open state,
/// selected value). During reconciliation, the composite widget builds its content and
/// the framework reconciles that content as a child of the composite node.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record PickerWidget(IReadOnlyList&lt;string&gt; Items) : CompositeWidget&lt;PickerNode&gt;
/// {
///     public int SelectedIndex { get; init; }
///     
///     protected override Task&lt;Hex1bWidget&gt; BuildContentAsync(PickerNode node, ReconcileContext context)
///     {
///         // Build button that shows current selection
///         // When clicked, show popup list
///         return Task.FromResult&lt;Hex1bWidget&gt;(
///             new ButtonWidget(Items[node.SelectedIndex])
///                 .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildPickerList()))
///         );
///     }
/// }
/// </code>
/// </example>
public abstract record CompositeWidget<TNode> : Hex1bWidget
    where TNode : CompositeNode, new()
{
    /// <summary>
    /// Builds the content widget tree for this composite widget.
    /// Called during reconciliation to determine what widgets to render.
    /// </summary>
    /// <param name="node">The composite node managing this widget's state.</param>
    /// <param name="context">The reconciliation context.</param>
    /// <returns>A task that resolves to the widget representing this composite's content.</returns>
    /// <remarks>
    /// This method may be async to support loading data or other async operations
    /// when building the content. The returned widget tree will be reconciled as
    /// a child of the composite node.
    /// </remarks>
    protected abstract Task<Hex1bWidget> BuildContentAsync(TNode node, ReconcileContext context);

    /// <summary>
    /// Called during reconciliation to update the node's properties from the widget.
    /// Override this to copy widget properties to the node before building content.
    /// </summary>
    /// <param name="node">The node to update.</param>
    /// <remarks>
    /// This is called after the node is created/reused but before <see cref="BuildContentAsync"/>.
    /// Use this to sync immutable widget properties to mutable node properties.
    /// </remarks>
    protected virtual void UpdateNode(TNode node)
    {
    }

    internal sealed override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TNode ?? new TNode();
        
        // Let the subclass update node properties
        UpdateNode(node);
        
        // Store reference to source widget
        node.SourceWidget = this;
        
        // Build the content widget tree
        var contentWidget = await BuildContentAsync(node, context);
        
        // Reconcile the content as a child of this composite node
        node.ContentChild = await context.ReconcileChildAsync(node.ContentChild, contentWidget, node);
        
        return node;
    }

    internal sealed override Type GetExpectedNodeType() => typeof(TNode);
}

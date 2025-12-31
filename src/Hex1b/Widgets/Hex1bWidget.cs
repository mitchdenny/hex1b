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

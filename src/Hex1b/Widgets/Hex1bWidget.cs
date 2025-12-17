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
    /// Creates or updates a node from this widget.
    /// </summary>
    /// <param name="existingNode">The existing node to update, or null to create a new one.</param>
    /// <param name="context">The reconciliation context with helpers for child reconciliation and focus.</param>
    /// <returns>The reconciled node.</returns>
    internal abstract Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context);

    /// <summary>
    /// Gets the expected node type for this widget. Used to determine if an existing node can be reused.
    /// </summary>
    internal abstract Type GetExpectedNodeType();
}

/// <summary>
/// How text should handle horizontal overflow.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// Text extends beyond bounds (default, for backward compatibility).
    /// Clipping is handled by parent LayoutNode if present.
    /// </summary>
    Overflow,
    
    /// <summary>
    /// Text wraps to next line when it exceeds available width.
    /// This affects the measured height of the node.
    /// </summary>
    Wrap,
    
    /// <summary>
    /// Text is truncated with ellipsis when it exceeds available width.
    /// </summary>
    Ellipsis,
}

public sealed record TextBlockWidget(string Text, TextOverflow Overflow = TextOverflow.Overflow) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBlockNode ?? new TextBlockNode();
        node.Text = Text;
        node.Overflow = Overflow;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(TextBlockNode);
}

public sealed record TextBoxWidget(TextBoxState? State = null) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBoxNode ?? new TextBoxNode();
        
        // Only override state if explicitly provided (controlled mode)
        // Otherwise, node keeps its own internal state (uncontrolled mode)
        if (State != null)
        {
            node.State = State;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(TextBoxNode);
}

public sealed record VStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as VStackNode ?? new VStackNode();

        // Create child context with vertical layout axis
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        
        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = childContext.ReconcileChild(existingChild, Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(VStackNode);
}

public sealed record HStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as HStackNode ?? new HStackNode();

        // Create child context with horizontal layout axis
        var childContext = context.WithLayoutAxis(LayoutAxis.Horizontal);
        
        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = childContext.ReconcileChild(existingChild, Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(HStackNode);
}

/// <summary>
/// A widget that draws a box border around its child content.
/// </summary>
public sealed record BorderWidget(Hex1bWidget Child, string? Title = null) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BorderNode ?? new BorderNode();
        node.Child = context.ReconcileChild(node.Child, Child, node);
        node.Title = Title;
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(BorderNode);
}

/// <summary>
/// A widget that provides a styled background for its child content.
/// </summary>
public sealed record PanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as PanelNode ?? new PanelNode();
        node.Child = context.ReconcileChild(node.Child, Child, node);
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(PanelNode);
}

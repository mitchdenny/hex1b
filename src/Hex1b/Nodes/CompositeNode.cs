using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Base class for nodes that manage composite widgets.
/// Composite nodes hold state and delegate rendering to their content child.
/// </summary>
/// <remarks>
/// <para>
/// CompositeNode acts as a transparent container - it delegates measure, arrange,
/// and render operations to its content child. Subclasses can override these methods
/// to add additional behavior (e.g., wrapping content, handling input).
/// </para>
/// <para>
/// State that must survive re-renders (like popup visibility, selection index)
/// should be stored as properties on the subclass.
/// </para>
/// </remarks>
public abstract class CompositeNode : Hex1bNode
{
    /// <summary>
    /// The reconciled content child node.
    /// This is the root of the widget tree built by the composite widget.
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }
    
    /// <summary>
    /// Reference to the source widget for typed event args.
    /// </summary>
    internal Hex1bWidget? SourceWidget { get; set; }

    /// <summary>
    /// Returns the content child as the only child of this node.
    /// This enables focus traversal through composite widgets.
    /// </summary>
    public override IReadOnlyList<Hex1bNode> GetChildren()
    {
        return ContentChild != null ? [ContentChild] : [];
    }

    /// <summary>
    /// Measures the content child and returns its size.
    /// </summary>
    public override Size Measure(Constraints constraints)
    {
        if (ContentChild == null)
        {
            return constraints.Constrain(Size.Zero);
        }
        
        return ContentChild.Measure(constraints);
    }

    /// <summary>
    /// Arranges the content child to fill the given bounds.
    /// </summary>
    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);
        ContentChild?.Arrange(rect);
    }

    /// <summary>
    /// Renders the content child.
    /// </summary>
    public override void Render(Hex1bRenderContext context)
    {
        ContentChild?.Render(context);
    }

    /// <summary>
    /// Composite nodes are NOT themselves focusable - focus passes through to children.
    /// This ensures the input router continues traversing into ContentChild to find
    /// the actual focusable node (which has the input bindings).
    /// </summary>
    public override bool IsFocusable => false;

    /// <summary>
    /// Composite nodes are never themselves focused - focus state exists on the content child.
    /// The path builder should continue through GetChildren() to find the focused child.
    /// </summary>
    public override bool IsFocused
    {
        get => false;
        set
        {
            // Setting focus on composite node propagates to content child
            if (ContentChild != null)
            {
                ContentChild.IsFocused = value;
            }
        }
    }
    
    /// <summary>
    /// Returns focusable nodes from the content child.
    /// This enables the focus ring to find the actual focusable nodes inside composite widgets.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}

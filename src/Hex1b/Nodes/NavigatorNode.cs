using System.Diagnostics.CodeAnalysis;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node that renders the current screen from a NavigatorState.
/// The navigator delegates all rendering and input to the current child.
/// </summary>
[Experimental("HEX1B001")]
public sealed class NavigatorNode : Hex1bNode
{
    /// <summary>
    /// The navigation state that manages the route stack.
    /// </summary>
    public NavigatorState State { get; set; } = null!;

    /// <summary>
    /// The currently rendered child node (from the current route).
    /// </summary>
    public Hex1bNode? CurrentChild { get; set; }

    /// <summary>
    /// Tracks the current route ID to detect navigation changes.
    /// </summary>
    public string? CurrentRouteId { get; set; }

    /// <inheritdoc />
    public override bool IsFocusable => false;

    /// <inheritdoc />
    public override Size Measure(Constraints constraints)
    {
        if (CurrentChild == null)
            return constraints.Constrain(Size.Zero);

        return CurrentChild.Measure(constraints);
    }

    /// <inheritdoc />
    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        CurrentChild?.Arrange(bounds);
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        // Use RenderChild for automatic caching support
        if (CurrentChild != null)
        {
            context.RenderChild(CurrentChild);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (CurrentChild != null)
        {
            foreach (var node in CurrentChild.GetFocusableNodes())
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (CurrentChild != null) yield return CurrentChild;
    }
}

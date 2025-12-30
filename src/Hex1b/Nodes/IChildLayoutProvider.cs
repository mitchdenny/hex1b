namespace Hex1b.Nodes;

/// <summary>
/// Interface for nodes that provide layout/clipping context for their children.
/// </summary>
/// <remarks>
/// This is used during partial re-renders when <see cref="Hex1bApp"/> traverses
/// to dirty children without fully rendering the parent node. Nodes that have
/// child panes with their own clipping regions (like <see cref="SplitterNode"/>)
/// implement this interface to ensure proper clipping during traversal.
/// </remarks>
public interface IChildLayoutProvider
{
    /// <summary>
    /// Gets the layout provider to use when rendering a specific child.
    /// </summary>
    /// <param name="child">The child node being rendered.</param>
    /// <returns>
    /// The layout provider to use for this child, or <c>null</c> if no special
    /// layout provider is needed.
    /// </returns>
    ILayoutProvider? GetChildLayoutProvider(Hex1bNode child);
}

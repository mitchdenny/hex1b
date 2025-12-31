using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that aligns its child within the available space.
/// </summary>
/// <param name="Child">The child widget to align.</param>
/// <param name="Alignment">The alignment flags specifying horizontal and/or vertical alignment.</param>
/// <remarks>
/// <para>
/// AlignWidget expands to fill its parent container and positions the child
/// based on the specified alignment flags. This is useful for centering content,
/// positioning status bars at the bottom, or right-aligning buttons.
/// </para>
/// <para>
/// The alignment can combine horizontal (Left, HCenter, Right) and vertical
/// (Top, VCenter, Bottom) flags. Convenience combinations like Center, TopRight,
/// and BottomLeft are provided.
/// </para>
/// </remarks>
/// <example>
/// <para>Center a text widget:</para>
/// <code>
/// ctx.Align(Alignment.Center, ctx.Text("Hello!"))
/// </code>
/// <para>Or use the convenience method:</para>
/// <code>
/// ctx.Center(ctx.Text("Hello!"))
/// </code>
/// </example>
/// <seealso cref="AlignExtensions"/>
/// <seealso cref="Alignment"/>
public sealed record AlignWidget(Hex1bWidget Child, Alignment Alignment) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as AlignNode ?? new AlignNode();
        
        if (node.Alignment != Alignment)
        {
            node.MarkDirty();
        }
        
        node.Alignment = Alignment;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(AlignNode);
}

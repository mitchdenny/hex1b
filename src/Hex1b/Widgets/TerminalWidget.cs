using Hex1b.Nodes;
using Hex1b.Terminal;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays the output of an embedded terminal via a presentation adapter.
/// Allows a Hex1bApp to host other terminal experiences (like tmux).
/// </summary>
/// <param name="PresentationAdapter">The presentation adapter connected to the embedded terminal.</param>
/// <remarks>
/// <para>
/// This widget renders the screen buffer of an embedded terminal via the
/// <see cref="Hex1bAppPresentationAdapter"/>, allowing you to create nested 
/// terminal experiences.
/// </para>
/// <para>
/// For the first pass, input is not forwarded to the embedded terminal,
/// but this can be added in future versions.
/// </para>
/// </remarks>
public sealed record TerminalWidget(Hex1bAppPresentationAdapter PresentationAdapter) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TerminalNode ?? new TerminalNode();
        
        // Mark dirty if presentation adapter changed
        if (node.PresentationAdapter != PresentationAdapter)
        {
            node.MarkDirty();
        }
        
        node.PresentationAdapter = PresentationAdapter;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(TerminalNode);
}

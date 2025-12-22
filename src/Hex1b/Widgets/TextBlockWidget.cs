namespace Hex1b.Widgets;

public sealed record TextBlockWidget(string Text, TextOverflow Overflow = TextOverflow.Overflow) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBlockNode ?? new TextBlockNode();
        
        // Mark dirty if properties changed
        if (node.Text != Text || node.Overflow != Overflow)
        {
            node.MarkDirty();
        }
        
        node.Text = Text;
        node.Overflow = Overflow;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(TextBlockNode);
}

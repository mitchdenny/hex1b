using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A floating window widget with title bar and content area.
/// Windows are typically opened via <see cref="WindowManager.Open"/> rather than declared directly.
/// </summary>
/// <param name="Entry">The window entry from the WindowManager.</param>
internal sealed record WindowWidget(WindowEntry Entry) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as WindowNode ?? new WindowNode();

        // Update from entry
        node.Entry = Entry;
        node.Title = Entry.Title;
        node.IsResizable = Entry.IsResizable;
        node.IsModal = Entry.IsModal;

        // Build and reconcile the content
        var contentWidget = Entry.ContentBuilder();
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Content = await childContext.ReconcileChildAsync(node.Content, contentWidget, node);

        // Link entry back to node
        Entry.Node = node;

        // Focus first focusable in new windows
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

    internal override Type GetExpectedNodeType() => typeof(WindowNode);
}

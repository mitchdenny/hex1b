using Hex1b.Documents;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget that renders a code block using the Editor widget in readonly mode.
/// Caches the <see cref="EditorState"/> across frames to avoid infinite re-render loops.
/// </summary>
internal sealed record MarkdownCodeBlockWidget(string Content) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(
        Hex1bNode? existingNode, ReconcileContext context)
    {
        // Use a simple container node to hold the cached state and the editor child
        var node = existingNode as MarkdownCodeBlockNode ?? new MarkdownCodeBlockNode();

        // Only recreate the editor state when content changes
        if (node.CachedContent != Content)
        {
            node.CachedContent = Content;
            var trimmed = Content.TrimEnd('\n', '\r');
            node.CachedState = new EditorState(new Hex1bDocument(trimmed)) { IsReadOnly = true };
            node.LineCount = Math.Max(1, trimmed.Split('\n').Length);
            node.MarkDirty();
        }

        var editorWidget = new EditorWidget(node.CachedState!)
            .LineNumbers()
            .FixedHeight(node.LineCount)
            .WithInputBindings(bindings =>
            {
                // Remove scroll bindings so scroll events pass through to the
                // parent ScrollPanel. These editors are sized exactly to their
                // content, so scrolling within them is meaningless.
                bindings.Remove(EditorWidget.ScrollUp);
                bindings.Remove(EditorWidget.ScrollDown);
                bindings.Remove(EditorWidget.ScrollLeft);
                bindings.Remove(EditorWidget.ScrollRight);
            });

        node.EditorChild = await context.ReconcileChildAsync(
            node.EditorChild, editorWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(MarkdownCodeBlockNode);
}

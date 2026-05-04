using System.Text;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="SelectionPanelWidget"/>.
/// </summary>
/// <remarks>
/// Layout, focus, and input remain delegated to the wrapped child. When a
/// <see cref="SnapshotHandler"/> is supplied, the node also registers a global
/// binding (default <c>Ctrl+Shift+S</c>, action id
/// <see cref="SelectionPanelWidget.Snapshot"/>) that calls
/// <see cref="SnapshotText"/> and invokes the handler with the result.
/// </remarks>
public sealed class SelectionPanelNode : Hex1bNode
{
    /// <summary>
    /// The child node wrapped by this panel.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Optional handler invoked with <see cref="SnapshotText"/> when the snapshot
    /// action fires. When <c>null</c>, no binding is registered.
    /// </summary>
    public Func<string, Task>? SnapshotHandler { get; set; }

    public override bool IsFocusable => false;

    public override bool IsFocused
    {
        get => false;
        set
        {
            if (Child != null)
                Child.IsFocused = value;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (SnapshotHandler is null)
        {
            return;
        }

        // Global because the SelectionPanel itself isn't focusable and the
        // user's focus typically lives outside its subtree (e.g. a TextBox
        // pinned below the scroll panel). Keeping this on the node — rather
        // than a free-floating app binding — means the binding only exists
        // when there is actually a panel registered to receive the snapshot.
        bindings.Ctrl().Shift().Key(Hex1bKey.S).Global().Triggers(
            SelectionPanelWidget.Snapshot,
            async _ =>
            {
                var handler = SnapshotHandler;
                if (handler is not null)
                {
                    await handler(SnapshotText());
                }
            },
            "Snapshot SelectionPanel content");
    }

    /// <summary>
    /// Walks the wrapped subtree and returns a plain-text snapshot of the
    /// content found inside. This is a proof-of-concept extractor: it harvests
    /// text from the most common text-bearing node types and joins them with
    /// newlines. A future copy-mode implementation will replace this with a
    /// cell-level read of the rendered surface.
    /// </summary>
    public string SnapshotText()
    {
        var sb = new StringBuilder();
        if (Child is not null)
        {
            CollectText(Child, sb);
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void CollectText(Hex1bNode node, StringBuilder sb)
    {
        switch (node)
        {
            case MarkdownNode md when !string.IsNullOrEmpty(md.Source):
                sb.AppendLine(md.Source);
                // Don't recurse — the markdown source already represents
                // every inline text block the renderer would produce.
                return;

            case TextBlockNode tb when !string.IsNullOrEmpty(tb.Text):
                sb.AppendLine(tb.Text);
                return;

            case BorderNode b when !string.IsNullOrEmpty(b.Title):
                sb.Append("--- ").Append(b.Title).AppendLine(" ---");
                break;
        }

        foreach (var child in node.GetChildren())
        {
            CollectText(child, sb);
        }
    }
}

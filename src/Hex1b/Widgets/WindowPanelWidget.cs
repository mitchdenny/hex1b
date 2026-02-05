using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container panel that hosts floating windows.
/// Windows are managed through the <see cref="WindowManager"/> and rendered on top of the main content.
/// </summary>
/// <param name="Content">The main content widget displayed behind windows.</param>
/// <remarks>
/// <para>
/// WindowPanel provides the bounded area within which floating windows can be positioned and dragged.
/// Windows cannot extend beyond the panel's bounds (they are clamped to fit).
/// </para>
/// <para>
/// Access the window manager from event handlers via <c>e.Context.Windows</c>:
/// <code>
/// ctx.Button("Open").OnClick(e =&gt; {
///     e.Context.Windows.Open("settings", "Settings", 
///         content: c =&gt; c.Text("Hello"));
/// });
/// </code>
/// </para>
/// </remarks>
public sealed record WindowPanelWidget(Hex1bWidget Content) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as WindowPanelNode ?? new WindowPanelNode();

        // Reconcile main content
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Content = await childContext.ReconcileChildAsync(node.Content, Content, node);

        // Reconcile windows from the WindowManager
        await node.ReconcileWindowsAsync(context);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(WindowPanelNode);
}

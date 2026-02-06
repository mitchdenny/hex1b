using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container panel that hosts floating windows.
/// Windows are managed through the <see cref="WindowManager"/> and rendered on top of the main content.
/// </summary>
/// <param name="Content">The main content widget displayed behind windows.</param>
/// <param name="Name">Optional name for this panel. Required when using multiple WindowPanels.</param>
/// <remarks>
/// <para>
/// WindowPanel provides the bounded area within which floating windows can be positioned and dragged.
/// By default, windows are clamped to the panel bounds. Set <see cref="AllowOutOfBounds"/> to true
/// to allow windows to be dragged outside the panel, with scrollbars appearing for navigation.
/// </para>
/// <para>
/// Access the window manager from event handlers via <c>e.Context.Windows</c>:
/// <code>
/// ctx.Button("Open").OnClick(e =&gt; {
///     e.Context.Windows.Open("settings", "Settings", 
///         () =&gt; c.Text("Hello"));
/// });
/// </code>
/// </para>
/// <para>
/// When using multiple WindowPanels, each must have a unique name:
/// <code>
/// ctx.WindowPanel("editor", content => ...);
/// ctx.WindowPanel("preview", content => ...);
/// // Access via: e.Windows["editor"].Open(...)
/// </code>
/// </para>
/// </remarks>
public sealed record WindowPanelWidget(Hex1bWidget Content, string? Name = null) : Hex1bWidget
{
    /// <summary>
    /// Whether windows can be moved outside the panel bounds.
    /// When true, scrollbars appear to allow panning to out-of-bounds windows.
    /// </summary>
    public bool AllowOutOfBounds { get; init; }

    /// <summary>
    /// Optional background widget that renders behind all content and windows.
    /// This widget is purely decorative and does not receive focus or input.
    /// </summary>
    public Hex1bWidget? Background { get; init; }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var isNew = existingNode is not WindowPanelNode;
        var node = existingNode as WindowPanelNode ?? new WindowPanelNode();

        // Register with the app-level registry on first creation
        if (isNew && context.WindowManagerRegistry != null)
        {
            node.Name = Name;
            context.WindowManagerRegistry.Register(node.Windows, Name);
        }

        // Update panel properties
        node.AllowOutOfBounds = AllowOutOfBounds;

        // Reconcile background (decorative, no focus handling)
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        if (Background != null)
        {
            node.BackgroundNode = await childContext.ReconcileChildAsync(node.BackgroundNode, Background, node);
        }
        else
        {
            node.BackgroundNode = null;
        }

        // Reconcile main content
        node.Content = await childContext.ReconcileChildAsync(node.Content, Content, node);

        // Reconcile windows from the WindowManager
        await node.ReconcileWindowsAsync(context);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(WindowPanelNode);
}

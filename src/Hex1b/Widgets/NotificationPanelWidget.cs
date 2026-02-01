using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification panel widget that hosts notifications and displays them as floating overlays
/// or in a docked drawer.
/// </summary>
/// <remarks>
/// <para>
/// This is a placeholder implementation for Phase 2. The full implementation will include:
/// <list type="bullet">
///   <item><description>Floating notification cards as overlays</description></item>
///   <item><description>Expandable drawer showing all notifications</description></item>
///   <item><description>Configurable max visible floating notifications</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record NotificationPanelWidget : Hex1bWidget
{
    /// <summary>
    /// The content to display in the main area (notifications overlay on top of this).
    /// </summary>
    internal Hex1bWidget? Content { get; init; }

    /// <summary>
    /// Maximum number of notifications to show floating at once.
    /// </summary>
    public int MaxFloating { get; init; } = 3;

    /// <summary>
    /// Sets the content that notifications will overlay.
    /// </summary>
    /// <param name="content">The main content widget.</param>
    public NotificationPanelWidget WithContent(Hex1bWidget content)
        => this with { Content = content };

    /// <summary>
    /// Sets the maximum number of floating notifications.
    /// </summary>
    /// <param name="max">Maximum floating notifications (default: 3).</param>
    public NotificationPanelWidget WithMaxFloating(int max)
        => this with { MaxFloating = max };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NotificationPanelNode ?? new NotificationPanelNode();
        node.MaxFloating = MaxFloating;

        // Reconcile content
        if (Content != null)
        {
            node.Content = await context.ReconcileChildAsync(node.Content, Content, node);
        }
        else
        {
            node.Content = null;
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationPanelNode);
}

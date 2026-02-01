using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification panel widget that hosts notifications and displays them as floating overlays
/// or in a docked drawer.
/// </summary>
/// <remarks>
/// <para>
/// The NotificationPanel can use either an internal or external NotificationStack:
/// <list type="bullet">
///   <item><description>Internal: The panel manages its own stack (default)</description></item>
///   <item><description>External: Pass a NotificationStack to share state with the rest of the app</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record NotificationPanelWidget : Hex1bWidget
{
    /// <summary>
    /// Animation interval for progress bar updates (50ms = 20fps).
    /// </summary>
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The content to display in the main area (notifications overlay on top of this).
    /// </summary>
    internal Hex1bWidget? Content { get; init; }

    /// <summary>
    /// An external notification stack to use instead of the internal one.
    /// When set, the panel uses this stack for all notifications.
    /// </summary>
    internal NotificationStack? ExternalStack { get; init; }

    /// <summary>
    /// Maximum number of notifications to show floating at once.
    /// </summary>
    public int MaxFloating { get; init; } = 3;

    /// <summary>
    /// Horizontal offset from the right edge for floating notifications.
    /// </summary>
    public int OffsetX { get; init; } = 2;

    /// <summary>
    /// Vertical offset from the top edge for floating notifications.
    /// </summary>
    public int OffsetY { get; init; } = 1;

    /// <summary>
    /// Whether to enable animation for progress bars.
    /// </summary>
    public bool EnableAnimation { get; init; } = true;

    /// <summary>
    /// Sets the content that notifications will overlay.
    /// </summary>
    /// <param name="content">The main content widget.</param>
    public NotificationPanelWidget WithContent(Hex1bWidget content)
        => this with { Content = content };

    /// <summary>
    /// Uses an external notification stack instead of an internal one.
    /// This allows sharing notification state with the rest of the app.
    /// </summary>
    /// <param name="stack">The notification stack to use.</param>
    public NotificationPanelWidget WithStack(NotificationStack stack)
        => this with { ExternalStack = stack };

    /// <summary>
    /// Sets the maximum number of floating notifications.
    /// </summary>
    /// <param name="max">Maximum floating notifications (default: 3).</param>
    public NotificationPanelWidget WithMaxFloating(int max)
        => this with { MaxFloating = max };

    /// <summary>
    /// Sets the offset from the corner for floating notifications.
    /// </summary>
    /// <param name="x">Horizontal offset from right edge.</param>
    /// <param name="y">Vertical offset from top edge.</param>
    public NotificationPanelWidget WithOffset(int x, int y)
        => this with { OffsetX = x, OffsetY = y };

    /// <summary>
    /// Enables or disables animation for timeout progress bars.
    /// </summary>
    /// <param name="enable">True to enable animation (default), false to disable.</param>
    public NotificationPanelWidget WithAnimation(bool enable = true)
        => this with { EnableAnimation = enable };

    /// <summary>
    /// Returns animation interval to keep progress bars updating.
    /// </summary>
    internal override TimeSpan? GetEffectiveRedrawDelay()
    {
        // If explicitly set via RedrawDelay, use that
        if (RedrawDelay.HasValue)
        {
            return RedrawDelay;
        }

        // Enable animation for progress bars on notification cards
        return EnableAnimation ? AnimationInterval : null;
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NotificationPanelNode ?? new NotificationPanelNode();
        
        // Set external stack if provided
        node.SetExternalStack(ExternalStack);
        
        node.MaxFloating = MaxFloating;
        node.OffsetX = OffsetX;
        node.OffsetY = OffsetY;

        // Reconcile content
        if (Content != null)
        {
            node.Content = await context.ReconcileChildAsync(node.Content, Content, node);
        }
        else
        {
            node.Content = null;
        }

        // Reconcile notification cards for floating notifications
        var floating = node.Notifications.Floating;
        var visibleCount = Math.Min(floating.Count, MaxFloating);
        
        // Ensure card node list matches visible count
        while (node.CardNodes.Count < visibleCount)
        {
            node.CardNodes.Add(null!);
        }
        while (node.CardNodes.Count > visibleCount)
        {
            node.CardNodes.RemoveAt(node.CardNodes.Count - 1);
        }

        // Reconcile each visible floating notification card
        for (int i = 0; i < visibleCount; i++)
        {
            var notification = floating[i];
            var cardWidget = new NotificationCardWidget(notification, node.Notifications);
            var existingCard = node.CardNodes[i];
            node.CardNodes[i] = (NotificationCardNode)(await context.ReconcileChildAsync(existingCard, cardWidget, node))!;
        }

        // Reconcile drawer cards for all notifications (when drawer is expanded)
        if (node.IsDrawerExpanded)
        {
            var all = node.Notifications.All;
            
            // Ensure drawer card node list matches count
            while (node.DrawerCardNodes.Count < all.Count)
            {
                node.DrawerCardNodes.Add(null!);
            }
            while (node.DrawerCardNodes.Count > all.Count)
            {
                node.DrawerCardNodes.RemoveAt(node.DrawerCardNodes.Count - 1);
            }

            // Reconcile each drawer notification card
            for (int i = 0; i < all.Count; i++)
            {
                var notification = all[i];
                var cardWidget = new NotificationCardWidget(notification, node.Notifications);
                var existingCard = node.DrawerCardNodes[i];
                node.DrawerCardNodes[i] = (NotificationCardNode)(await context.ReconcileChildAsync(existingCard, cardWidget, node))!;
            }
        }
        else
        {
            // Clear drawer cards when not expanded
            node.DrawerCardNodes.Clear();
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationPanelNode);
}

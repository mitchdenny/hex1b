using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification panel widget that hosts notifications and displays them as floating overlays
/// or in a slide-out drawer.
/// </summary>
/// <remarks>
/// <para>
/// The notification panel is the container that manages notification display. It wraps your main
/// content and provides:
/// <list type="bullet">
///   <item><description><strong>Floating notifications:</strong> Appear in the top-right corner as overlay cards.</description></item>
///   <item><description><strong>Notification drawer:</strong> A slide-out panel showing all notifications.</description></item>
///   <item><description><strong>Timeout management:</strong> Automatically hides notifications after their timeout.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Typical layout pattern:</strong>
/// <code>
/// ctx.ZStack(z =&gt; [
///     z.VStack(v =&gt; [
///         v.HStack(bar =&gt; [
///             bar.Button("Menu"),
///             bar.NotificationIcon()
///         ]),
///         v.NotificationPanel(
///             v.Text("Your main content here")
///         ).Fill()
///     ])
/// ])
/// </code>
/// </para>
/// <para>
/// <strong>Posting notifications:</strong> Access the notification stack through the input context
/// and call <c>Post()</c>:
/// <code>
/// ctx.Button("Notify").OnClick(e =&gt; {
///     e.Context.Notifications.Post(
///         new Notification("Hello!", "This is a notification")
///             .Timeout(TimeSpan.FromSeconds(5))
///     );
/// })
/// </code>
/// </para>
/// <para>
/// <strong>Keyboard shortcuts:</strong>
/// <list type="bullet">
///   <item><description>Alt+N toggles the notification drawer.</description></item>
///   <item><description>Escape closes the drawer when open.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para>A complete notification-enabled application:</para>
/// <code>
/// var app = new Hex1bApp(ctx =&gt; ctx.ZStack(z =&gt; [
///     z.VStack(v =&gt; [
///         v.HStack(bar =&gt; [
///             bar.Button("File"),
///             bar.Text("").FillWidth(),
///             bar.NotificationIcon()
///         ]),
///         v.NotificationPanel(
///             v.Button("Show Notification").OnClick(e =&gt; {
///                 e.Context.Notifications.Post(
///                     new Notification("Task Complete", "Your task finished successfully")
///                         .Timeout(TimeSpan.FromSeconds(5))
///                         .PrimaryAction("View", async ctx =&gt; { /* view result */ })
///                 );
///             })
///         ).Fill()
///     ])
/// ]));
/// </code>
/// </example>
/// <seealso cref="Notification"/>
/// <seealso cref="NotificationStack"/>
/// <seealso cref="NotificationIconWidget"/>
/// <seealso cref="NotificationCardWidget"/>
public sealed record NotificationPanelWidget : Hex1bWidget
{
    /// <summary>
    /// Animation interval for progress bar updates (50ms = 20fps).
    /// </summary>
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The content widget that notifications will overlay. This is your main application content.
    /// </summary>
    internal Hex1bWidget? Content { get; init; }

    /// <summary>
    /// Maximum number of floating notifications to show at once. Defaults to 3.
    /// </summary>
    /// <remarks>
    /// Older notifications remain in the notification stack but are only visible when
    /// the user opens the drawer.
    /// </remarks>
    public int MaxFloating { get; init; } = 3;

    /// <summary>
    /// Horizontal offset from the right edge for floating notifications. Defaults to 2.
    /// </summary>
    public int OffsetX { get; init; } = 2;

    /// <summary>
    /// Vertical offset from the top edge for floating notifications. Defaults to 1.
    /// </summary>
    public int OffsetY { get; init; } = 1;

    /// <summary>
    /// Whether to enable animation for timeout progress bars. Defaults to true.
    /// </summary>
    /// <remarks>
    /// When enabled, progress bars smoothly animate as the notification timeout counts down.
    /// Disable for better performance or in environments where animation causes issues.
    /// </remarks>
    public bool EnableAnimation { get; init; } = true;

    /// <summary>
    /// Whether the drawer floats on top of content (true) or pushes content aside (false).
    /// Defaults to true (floating).
    /// </summary>
    public bool DrawerFloats { get; init; } = true;

    /// <summary>
    /// Sets the content widget that notifications will overlay.
    /// </summary>
    /// <param name="content">The main content widget.</param>
    /// <returns>A new widget instance with the content configured.</returns>
    public NotificationPanelWidget WithContent(Hex1bWidget content)
        => this with { Content = content };

    /// <summary>
    /// Sets whether the drawer floats on top of content or pushes content aside.
    /// </summary>
    /// <param name="floats">True for floating overlay (default), false to push content aside.</param>
    /// <returns>A new widget instance with the setting configured.</returns>
    public NotificationPanelWidget WithDrawerFloats(bool floats = true)
        => this with { DrawerFloats = floats };

    /// <summary>
    /// Sets the maximum number of floating notifications visible at once.
    /// </summary>
    /// <param name="max">Maximum floating notifications. Defaults to 3.</param>
    /// <returns>A new widget instance with the setting configured.</returns>
    /// <remarks>
    /// Additional notifications are queued and become visible as earlier ones time out or are dismissed.
    /// All notifications are always visible in the drawer.
    /// </remarks>
    public NotificationPanelWidget WithMaxFloating(int max)
        => this with { MaxFloating = max };

    /// <summary>
    /// Sets the offset from the corner for floating notifications.
    /// </summary>
    /// <param name="x">Horizontal offset from right edge in columns.</param>
    /// <param name="y">Vertical offset from top edge in rows.</param>
    /// <returns>A new widget instance with the offsets configured.</returns>
    public NotificationPanelWidget WithOffset(int x, int y)
        => this with { OffsetX = x, OffsetY = y };

    /// <summary>
    /// Enables or disables animation for timeout progress bars.
    /// </summary>
    /// <param name="enable">True to enable animation (default), false to disable.</param>
    /// <returns>A new widget instance with the setting configured.</returns>
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
        
        // Clear cached stack so it re-discovers from parent chain
        node.ClearCachedStack();
        
        node.MaxFloating = MaxFloating;
        node.OffsetX = OffsetX;
        node.OffsetY = OffsetY;
        node.DrawerFloats = DrawerFloats;

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
        // Note: Notifications property will find the parent ZStack's NotificationStack
        NotificationStack notifications;
        try
        {
            notifications = node.Notifications;
        }
        catch (InvalidOperationException)
        {
            // No notification host found yet (parent not set) - skip card reconciliation
            return node;
        }
        
        var floating = notifications.Floating;
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

        // Reconcile drawer backdrop and cards when drawer is expanded
        if (node.IsDrawerExpanded)
        {
            // Create a backdrop that catches outside clicks and collapses the drawer
            var backdropWidget = new BackdropWidget(null)
            {
                Style = BackdropStyle.Transparent,
                ClickAwayHandler = () =>
                {
                    node.Notifications.HidePanel();
                    node.MarkDirty();
                    return Task.CompletedTask;
                }
            };
            node.DrawerBackdrop = (BackdropNode)(await context.ReconcileChildAsync(node.DrawerBackdrop, backdropWidget, node))!;
            
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

            // Reconcile each drawer notification card (no progress bar in drawer)
            for (int i = 0; i < all.Count; i++)
            {
                var notification = all[i];
                var cardWidget = new NotificationCardWidget(notification, node.Notifications) { ShowProgressBar = false };
                var existingCard = node.DrawerCardNodes[i];
                node.DrawerCardNodes[i] = (NotificationCardNode)(await context.ReconcileChildAsync(existingCard, cardWidget, node))!;
            }
            
            // Create VStack to hold all drawer cards
            node.DrawerVStack ??= new VStackNode();
            node.DrawerVStack.Children.Clear();
            foreach (var card in node.DrawerCardNodes)
            {
                node.DrawerVStack.Children.Add(card);
                card.Parent = node.DrawerVStack;
            }
            
            // Create Scroll to wrap the VStack for overflow handling
            node.DrawerScroll ??= new ScrollNode();
            node.DrawerScroll.Child = node.DrawerVStack;
            node.DrawerScroll.Orientation = ScrollOrientation.Vertical;
            node.DrawerScroll.Parent = node;
            node.DrawerVStack.Parent = node.DrawerScroll;
        }
        else
        {
            // Clear backdrop, scroll, vstack, and drawer cards when not expanded
            node.DrawerBackdrop = null;
            node.DrawerScroll = null;
            node.DrawerVStack = null;
            node.DrawerCardNodes.Clear();
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationPanelNode);
}

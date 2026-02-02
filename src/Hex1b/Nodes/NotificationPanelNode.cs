using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationPanelWidget"/>.
/// Displays floating notifications and a drawer panel.
/// </summary>
/// <remarks>
/// <para>
/// NotificationPanelNode finds the nearest <see cref="INotificationHost"/> (typically a ZStackNode)
/// in its parent chain and uses that host's <see cref="NotificationStack"/>.
/// This allows the NotificationPanel to be placed anywhere in the widget tree while still
/// accessing the app-wide notification system.
/// </para>
/// <para>
/// Renders floating notifications as cards in the top-right corner of the panel.
/// Notifications stack vertically, newest on top.
/// When the drawer is expanded, shows all notifications (including timed-out) in a sidebar.
/// </para>
/// </remarks>
public sealed class NotificationPanelNode : Hex1bNode
{
    private NotificationStack? _cachedStack;

    /// <summary>
    /// Gets the notification stack from the nearest INotificationHost in the parent chain.
    /// Caches the result for performance.
    /// </summary>
    public NotificationStack Notifications
    {
        get
        {
            if (_cachedStack != null)
            {
                return _cachedStack;
            }

            // Walk up parent chain to find INotificationHost
            var current = Parent;
            while (current != null)
            {
                if (current is INotificationHost host)
                {
                    _cachedStack = host.Notifications;
                    return _cachedStack;
                }
                current = current.Parent;
            }

            throw new InvalidOperationException(
                "No notification host found. Ensure NotificationPanel is inside a ZStack.");
        }
    }

    /// <summary>
    /// Clears the cached notification stack reference.
    /// Called when the node is re-parented.
    /// </summary>
    internal void ClearCachedStack()
    {
        _cachedStack = null;
    }

    /// <summary>
    /// The main content node.
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// Maximum number of floating notifications to display.
    /// </summary>
    public int MaxFloating { get; set; } = 3;

    /// <summary>
    /// Horizontal offset from the right edge for floating notifications.
    /// </summary>
    public int OffsetX { get; set; } = 1;

    /// <summary>
    /// Vertical offset from the top edge for floating notifications.
    /// </summary>
    public int OffsetY { get; set; } = 1;

    /// <summary>
    /// Whether the drawer floats on top of content or docks beside it.
    /// </summary>
    public bool DrawerFloats { get; set; } = true;

    /// <summary>
    /// Notification card nodes for floating view. Managed by NotificationPanelWidget reconciliation.
    /// </summary>
    public List<NotificationCardNode> CardNodes { get; } = new();

    /// <summary>
    /// Notification card nodes for drawer view. Managed by NotificationPanelWidget reconciliation.
    /// </summary>
    public List<NotificationCardNode> DrawerCardNodes { get; } = new();

    /// <summary>
    /// Backdrop node used to capture clicks outside the drawer when expanded.
    /// </summary>
    public BackdropNode? DrawerBackdrop { get; set; }

    /// <summary>
    /// Scroll node for the drawer content when there are many notifications.
    /// </summary>
    public ScrollNode? DrawerScroll { get; set; }

    /// <summary>
    /// VStack node to hold drawer cards inside the scroll.
    /// </summary>
    public VStackNode? DrawerVStack { get; set; }

    /// <summary>
    /// Whether the notification drawer is expanded. Syncs with NotificationStack.IsPanelVisible.
    /// </summary>
    public bool IsDrawerExpanded
    {
        get => Notifications.IsPanelVisible;
        set => Notifications.IsPanelVisible = value;
    }

    /// <summary>
    /// Width of notification cards.
    /// </summary>
    private const int CardWidth = 40;

    /// <summary>
    /// Width of the drawer when expanded.
    /// </summary>
    private const int DrawerWidth = 42;

    /// <summary>
    /// Spacing between floating notification cards.
    /// Set to 0 because the half-height block borders provide visual separation.
    /// </summary>
    private const int FloatingCardSpacing = 0;
    
    /// <summary>
    /// Spacing between drawer cards.
    /// </summary>
    private const int DrawerCardSpacing = 0;

    /// <summary>
    /// The bounds of the drawer area when expanded.
    /// Used for click-outside detection.
    /// </summary>
    private Rect _drawerBounds;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Alt+N toggles the notification drawer
        bindings.Alt().Key(Hex1bKey.N).Action(ToggleDrawer, "Toggle notifications");
    }

    private Task ToggleDrawer(InputBindingActionContext ctx)
    {
        Notifications.TogglePanel();
        MarkDirty();
        return Task.CompletedTask;
    }

    public override Size Measure(Constraints constraints)
    {
        if (Content == null)
        {
            return Size.Zero;
        }

        return Content.Measure(constraints);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Arrange backdrop to fill entire bounds (captures clicks outside drawer)
        DrawerBackdrop?.Arrange(bounds);

        if (IsDrawerExpanded && !DrawerFloats)
        {
            // Drawer docks beside content - content gets reduced width
            var contentWidth = Math.Max(0, bounds.Width - DrawerWidth);
            Content?.Arrange(new Rect(bounds.X, bounds.Y, contentWidth, bounds.Height));

            // Arrange drawer cards on the right
            _drawerBounds = new Rect(bounds.X + contentWidth, bounds.Y, DrawerWidth, bounds.Height);
            ArrangeDrawerCards(_drawerBounds);
        }
        else
        {
            // Content gets full width - drawer/cards float on top
            Content?.Arrange(bounds);

            if (IsDrawerExpanded)
            {
                // Arrange drawer cards on the right (floating on top)
                var drawerX = bounds.X + bounds.Width - DrawerWidth;
                _drawerBounds = new Rect(drawerX, bounds.Y, DrawerWidth, bounds.Height);
                ArrangeDrawerCards(_drawerBounds);
            }
            else
            {
                // Clear drawer bounds when not expanded
                _drawerBounds = Rect.Zero;
                
                // Arrange floating notification cards in top-right corner
                ArrangeFloatingCards(bounds);
            }
        }
    }

    private void ArrangeFloatingCards(Rect bounds)
    {
        var floating = Notifications.Floating;
        var visibleCount = Math.Min(floating.Count, MaxFloating);

        // Position cards from top-right with offset, stacking downward
        var x = bounds.X + bounds.Width - CardWidth - OffsetX;
        var y = bounds.Y + OffsetY;

        for (int i = 0; i < visibleCount && i < CardNodes.Count; i++)
        {
            var card = CardNodes[i];

            // Measure and arrange the card
            var constraints = new Constraints(0, CardWidth, 0, bounds.Height / 2);
            var size = card.Measure(constraints);
            card.Arrange(new Rect(x, y, size.Width, size.Height));

            y += size.Height + FloatingCardSpacing;
        }
    }

    private void ArrangeDrawerCards(Rect drawerBounds)
    {
        // Arrange the scroll/vstack to fill the drawer content area (below header)
        var contentY = drawerBounds.Y + 2; // Leave room for header
        var contentHeight = drawerBounds.Height - 2;
        var contentBounds = new Rect(drawerBounds.X + 1, contentY, drawerBounds.Width - 2, contentHeight);
        
        if (DrawerScroll != null)
        {
            DrawerScroll.Measure(new Constraints(contentBounds.Width, contentBounds.Width, 0, contentHeight));
            DrawerScroll.Arrange(contentBounds);
        }
        else
        {
            // Fallback: arrange cards manually if scroll not set up
            var y = contentY;
            for (int i = 0; i < DrawerCardNodes.Count; i++)
            {
                var card = DrawerCardNodes[i];
                var constraints = new Constraints(0, drawerBounds.Width - 2, 0, drawerBounds.Height / 3);
                var size = card.Measure(constraints);
                card.Arrange(new Rect(drawerBounds.X + 1, y, size.Width, size.Height));
                y += size.Height + DrawerCardSpacing;
                if (y >= drawerBounds.Y + drawerBounds.Height - 1) break;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (IsDrawerExpanded)
        {
            // When drawer is expanded, content focusables should be yielded FIRST
            // so they have lower priority in hit testing (HitTest checks in reverse order).
            // This ensures drawer elements block clicks on content below.
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
            
            // Include backdrop so it catches clicks outside drawer content
            // (yielded after content but before drawer cards for proper layering)
            if (DrawerBackdrop != null)
            {
                foreach (var focusable in DrawerBackdrop.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
            
            // Drawer scroll/cards yielded last so they're checked first in hit testing
            if (DrawerScroll != null)
            {
                foreach (var focusable in DrawerScroll.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
            else
            {
                foreach (var card in DrawerCardNodes)
                {
                    foreach (var focusable in card.GetFocusableNodes())
                    {
                        yield return focusable;
                    }
                }
            }
        }
        else
        {
            // Floating notifications get focus priority (yielded first = checked last)
            // Content is yielded first so it has lower hit test priority
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
            
            // Floating cards are yielded last so they're checked first
            var floating = Notifications.Floating;
            var visibleCount = Math.Min(floating.Count, MaxFloating);

            for (int i = 0; i < visibleCount && i < CardNodes.Count; i++)
            {
                foreach (var focusable in CardNodes[i].GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render main content first
        if (Content != null)
        {
            context.RenderChild(Content);
        }

        if (IsDrawerExpanded)
        {
            // Render backdrop first (transparent, but handles click-outside)
            if (DrawerBackdrop != null)
            {
                context.RenderChild(DrawerBackdrop);
            }
            RenderDrawer(context);
        }
        else
        {
            RenderFloatingCards(context);
        }
    }

    private void RenderFloatingCards(Hex1bRenderContext context)
    {
        var floating = Notifications.Floating;
        var visibleCount = Math.Min(floating.Count, MaxFloating);

        for (int i = 0; i < visibleCount && i < CardNodes.Count; i++)
        {
            CardNodes[i].Render(context);
        }

        // If there are more notifications than visible, show overflow indicator
        if (floating.Count > MaxFloating)
        {
            var overflowCount = floating.Count - MaxFloating;
            var indicator = $"+{overflowCount} more";
            var x = Bounds.X + Bounds.Width - CardWidth - 1;

            // Position below the last visible card
            var lastCardBottom = CardNodes.Count > 0
                ? CardNodes[visibleCount - 1].Bounds.Y + CardNodes[visibleCount - 1].Bounds.Height
                : Bounds.Y + 1;

            context.SetCursorPosition(x, lastCardBottom);
            context.Write(indicator);
        }
    }

    private void RenderDrawer(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var bg = theme.Get(GlobalTheme.BackgroundColor);
        var titleColor = theme.Get(NotificationCardTheme.TitleColor);
        var bgAnsi = bg.ToBackgroundAnsi();
        var fgAnsi = titleColor.ToForegroundAnsi();
        var reset = theme.GetResetToGlobalCodes();

        var drawerX = Bounds.X + Bounds.Width - DrawerWidth;
        var drawerHeight = Bounds.Height;

        // Draw drawer background and header
        for (int row = 0; row < drawerHeight; row++)
        {
            context.SetCursorPosition(drawerX, Bounds.Y + row);
            if (row == 0)
            {
                // Header
                var header = $" Notifications ({Notifications.Count}) ";
                var padding = DrawerWidth - header.Length - 1;
                context.Write($"{fgAnsi}{bgAnsi}│{header}{new string(' ', Math.Max(0, padding))}{reset}");
            }
            else if (row == 1)
            {
                // Separator
                context.Write($"{bgAnsi}├{new string('─', DrawerWidth - 1)}{reset}");
            }
            else
            {
                // Empty row (scroll content renders on top)
                context.Write($"{bgAnsi}│{new string(' ', DrawerWidth - 1)}{reset}");
            }
        }

        // Render scroll node which contains the VStack of cards
        if (DrawerScroll != null)
        {
            context.RenderChild(DrawerScroll);
        }
        else
        {
            // Fallback: render cards directly
            foreach (var card in DrawerCardNodes)
            {
                if (card.Bounds.Y < Bounds.Y + Bounds.Height)
                {
                    card.Render(context);
                }
            }
        }

        // Show "no notifications" if empty
        if (Notifications.Count == 0)
        {
            var msg = "No notifications";
            context.SetCursorPosition(drawerX + (DrawerWidth - msg.Length) / 2, Bounds.Y + 3);
            context.Write($"{fgAnsi}{bgAnsi}{msg}{reset}");
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null)
        {
            yield return Content;
        }

        if (IsDrawerExpanded)
        {
            // Include backdrop for click-outside handling
            if (DrawerBackdrop != null)
            {
                yield return DrawerBackdrop;
            }
            
            // Include scroll node which contains the cards
            if (DrawerScroll != null)
            {
                yield return DrawerScroll;
            }
            else
            {
                foreach (var card in DrawerCardNodes)
                {
                    yield return card;
                }
            }
        }
        else
        {
            // Include visible floating card nodes as children
            var floating = Notifications.Floating;
            var visibleCount = Math.Min(floating.Count, MaxFloating);
            for (int i = 0; i < visibleCount && i < CardNodes.Count; i++)
            {
                yield return CardNodes[i];
            }
        }
    }
}

using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationPanelWidget"/>.
/// Implements <see cref="INotificationHost"/> to allow posting notifications from anywhere in the tree.
/// </summary>
/// <remarks>
/// Renders floating notifications as cards in the top-right corner of the panel.
/// Notifications stack vertically, newest on top.
/// When the drawer is expanded, shows all notifications (including timed-out) in a sidebar.
/// </remarks>
public sealed class NotificationPanelNode : Hex1bNode, INotificationHost
{
    private NotificationStack? _externalStack;
    private readonly NotificationStack _internalStack = new();

    /// <summary>
    /// The notification stack for this host. Uses external stack if set, otherwise internal.
    /// </summary>
    public NotificationStack Notifications => _externalStack ?? _internalStack;

    /// <summary>
    /// Sets an external notification stack to use instead of the internal one.
    /// </summary>
    internal void SetExternalStack(NotificationStack? stack)
    {
        _externalStack = stack;
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
    /// Notification card nodes for floating view. Managed by NotificationPanelWidget reconciliation.
    /// </summary>
    public List<NotificationCardNode> CardNodes { get; } = new();

    /// <summary>
    /// Notification card nodes for drawer view. Managed by NotificationPanelWidget reconciliation.
    /// </summary>
    public List<NotificationCardNode> DrawerCardNodes { get; } = new();

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
    /// Spacing between cards.
    /// </summary>
    private const int CardSpacing = 1;

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

        if (IsDrawerExpanded)
        {
            // Content gets reduced width when drawer is open
            var contentWidth = Math.Max(0, bounds.Width - DrawerWidth);
            Content?.Arrange(new Rect(bounds.X, bounds.Y, contentWidth, bounds.Height));

            // Arrange drawer cards on the right
            ArrangeDrawerCards(new Rect(bounds.X + contentWidth, bounds.Y, DrawerWidth, bounds.Height));
        }
        else
        {
            Content?.Arrange(bounds);

            // Arrange floating notification cards in top-right corner
            ArrangeFloatingCards(bounds);
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

            y += size.Height + CardSpacing;
        }
    }

    private void ArrangeDrawerCards(Rect drawerBounds)
    {
        var y = drawerBounds.Y + 2; // Leave room for header

        for (int i = 0; i < DrawerCardNodes.Count; i++)
        {
            var card = DrawerCardNodes[i];

            // Measure and arrange the card
            var constraints = new Constraints(0, drawerBounds.Width - 2, 0, drawerBounds.Height / 3);
            var size = card.Measure(constraints);
            card.Arrange(new Rect(drawerBounds.X + 1, y, size.Width, size.Height));

            y += size.Height + CardSpacing;

            // Stop if we run out of space
            if (y >= drawerBounds.Y + drawerBounds.Height - 1) break;
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (IsDrawerExpanded)
        {
            // Drawer cards get focus when drawer is open
            foreach (var card in DrawerCardNodes)
            {
                foreach (var focusable in card.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
        else
        {
            // Floating notifications get focus priority
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

        // Then content focusables
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
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
        var bg = theme.Get(NotificationCardTheme.BackgroundColor);
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
                // Empty row (cards render on top)
                context.Write($"{bgAnsi}│{new string(' ', DrawerWidth - 1)}{reset}");
            }
        }

        // Render drawer cards
        foreach (var card in DrawerCardNodes)
        {
            if (card.Bounds.Y < Bounds.Y + Bounds.Height)
            {
                card.Render(context);
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
            foreach (var card in DrawerCardNodes)
            {
                yield return card;
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

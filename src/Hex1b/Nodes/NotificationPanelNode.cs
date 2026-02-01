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
/// </remarks>
public sealed class NotificationPanelNode : Hex1bNode, INotificationHost
{
    /// <summary>
    /// The notification stack for this host.
    /// </summary>
    public NotificationStack Notifications { get; } = new();

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
    /// Notification card nodes for rendering. Managed by NotificationPanelWidget reconciliation.
    /// </summary>
    public List<NotificationCardNode> CardNodes { get; } = new();

    /// <summary>
    /// Width of notification cards.
    /// </summary>
    private const int CardWidth = 40;

    /// <summary>
    /// Spacing between cards.
    /// </summary>
    private const int CardSpacing = 1;

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

        Content?.Arrange(bounds);

        // Arrange floating notification cards in top-right corner
        ArrangeFloatingCards(bounds);
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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
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

        // Render floating notification cards on top
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

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null)
        {
            yield return Content;
        }

        // Include visible card nodes as children
        var floating = Notifications.Floating;
        var visibleCount = Math.Min(floating.Count, MaxFloating);
        for (int i = 0; i < visibleCount && i < CardNodes.Count; i++)
        {
            yield return CardNodes[i];
        }
    }
}

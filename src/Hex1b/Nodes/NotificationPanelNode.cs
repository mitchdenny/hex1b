using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationPanelWidget"/>.
/// Implements <see cref="INotificationHost"/> to allow posting notifications from anywhere in the tree.
/// </summary>
/// <remarks>
/// This is a placeholder implementation for Phase 2. Currently just renders content and hosts
/// the notification stack. Full implementation will add floating overlays and drawer.
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
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // TODO: When floating notifications exist, return their focusables first
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
        if (Content != null)
        {
            context.RenderChild(Content);
        }

        // TODO: Render floating notification cards on top
        // For now, just render a placeholder indicator if there are notifications
        var floatingCount = Notifications.FloatingCount;
        if (floatingCount > 0)
        {
            // Render a simple indicator in top-right corner
            var indicator = $"🔔 {floatingCount}";
            var x = Bounds.X + Bounds.Width - indicator.Length - 1;
            var y = Bounds.Y;
            if (x >= 0 && y >= 0)
            {
                context.SetCursorPosition(x, y);
                context.Write(indicator);
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null)
        {
            yield return Content;
        }
    }
}

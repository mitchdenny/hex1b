using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification card widget that displays a single notification with title, body, and actions.
/// </summary>
/// <remarks>
/// <para>
/// The notification card renders as a bordered box with:
/// <list type="bullet">
///   <item><description>Title row with optional dismiss button</description></item>
///   <item><description>Body text (if present)</description></item>
///   <item><description>Action buttons (primary + secondary dropdown)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record NotificationCardWidget : Hex1bWidget
{
    /// <summary>
    /// The notification to display.
    /// </summary>
    public Notification Notification { get; }

    /// <summary>
    /// The notification stack this card belongs to (for dismiss operations).
    /// </summary>
    internal NotificationStack Stack { get; init; }

    /// <summary>
    /// Creates a notification card for the specified notification.
    /// </summary>
    /// <param name="notification">The notification to display.</param>
    /// <param name="stack">The notification stack for dismiss operations.</param>
    public NotificationCardWidget(Notification notification, NotificationStack stack)
    {
        Notification = notification;
        Stack = stack;
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NotificationCardNode ?? new NotificationCardNode();

        // Check if notification content changed
        if (node.Title != Notification.Title || node.Body != Notification.Body)
        {
            node.MarkDirty();
        }

        node.Title = Notification.Title;
        node.Body = Notification.Body;
        node.Notification = Notification;
        node.Stack = Stack;
        node.PrimaryAction = Notification.PrimaryActionValue;
        node.SecondaryActions = Notification.SecondaryActions;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationCardNode);
}

using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification card widget that displays a single notification with title, body, and actions.
/// </summary>
/// <remarks>
/// <para>
/// The notification card composes child widgets:
/// <list type="bullet">
///   <item><description>Title row with dismiss ButtonNode</description></item>
///   <item><description>Body text (if present)</description></item>
///   <item><description>Action button (SplitButtonNode with primary + secondary actions)</description></item>
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
    /// Whether to show the timeout progress bar. Default is true.
    /// Set to false for drawer cards where the countdown isn't relevant.
    /// </summary>
    public bool ShowProgressBar { get; init; } = true;

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

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
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
        node.ShowProgressBar = ShowProgressBar;

        // Reconcile dismiss button
        var dismissWidget = new ButtonWidget("×")
            .OnClick(async e =>
            {
                if (Notification.DismissHandler != null)
                {
                    var eventCtx = new NotificationEventContext(Notification, Stack, e.Context.CancellationToken);
                    await Notification.DismissHandler(eventCtx);
                }
                Stack.Dismiss(Notification);
            });
        node.DismissButton = (ButtonNode?)await context.ReconcileChildAsync(node.DismissButton, dismissWidget, node);

        // Reconcile action button if there's a primary action
        if (Notification.PrimaryActionValue != null)
        {
            var actionWidget = new SplitButtonWidget(Notification.PrimaryActionValue.Label)
                .OnPrimaryClick(async e =>
                {
                    var actionCtx = new NotificationActionContext(Notification, Stack, e.Context.CancellationToken, e.Context);
                    await Notification.PrimaryActionValue.Handler(actionCtx);
                });

            // Add secondary actions
            foreach (var secondary in Notification.SecondaryActions)
            {
                actionWidget = actionWidget.WithSecondaryAction(secondary.Label, async e =>
                {
                    var actionCtx = new NotificationActionContext(Notification, Stack, e.Context.CancellationToken, e.Context);
                    await secondary.Handler(actionCtx);
                });
            }

            node.ActionButton = (SplitButtonNode?)await context.ReconcileChildAsync(node.ActionButton, actionWidget, node);
        }
        else
        {
            node.ActionButton = null;
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationCardNode);
}

namespace Hex1b;

/// <summary>
/// Represents a notification that can be displayed to the user.
/// Notifications are mutable state objects - modify properties directly and the UI will update.
/// </summary>
/// <remarks>
/// <para>
/// Notifications have a lifecycle: they are posted to a <see cref="NotificationStack"/>,
/// appear as floating overlays, optionally time out (hiding from floating view but remaining
/// in the notification panel), and can be dismissed entirely.
/// </para>
/// <para>
/// Use the fluent API to configure actions and lifecycle handlers:
/// <code>
/// var notification = new Notification("File saved", "document.txt saved successfully")
///     .Timeout(TimeSpan.FromSeconds(5))
///     .PrimaryAction("View", async ctx => await OpenFileAsync())
///     .SecondaryAction("Undo", async ctx => await UndoSaveAsync())
///     .OnDismiss(async ctx => await CleanupAsync());
///     
/// e.Notifications.Post(notification);
/// </code>
/// </para>
/// </remarks>
public sealed class Notification
{
    private readonly List<NotificationAction> _secondaryActions = new();

    /// <summary>
    /// Creates a new notification with the specified title.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="body">Optional body text with additional details.</param>
    public Notification(string title, string? body = null)
    {
        Title = title;
        Body = body;
        CreatedAt = DateTimeOffset.Now;
    }

    /// <summary>
    /// The notification title. Displayed prominently.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Optional body text with additional details.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Optional timeout after which the notification hides from floating view.
    /// The notification remains in the notification panel until explicitly dismissed.
    /// If null, the notification floats indefinitely until dismissed.
    /// </summary>
    public TimeSpan? TimeoutDuration { get; set; }

    /// <summary>
    /// When this notification was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// The primary action button. Displayed as the main button on the notification.
    /// </summary>
    public NotificationAction? PrimaryActionValue { get; private set; }

    /// <summary>
    /// Secondary actions. Displayed in a dropdown menu next to the primary action.
    /// </summary>
    public IReadOnlyList<NotificationAction> SecondaryActions => _secondaryActions;

    /// <summary>
    /// Handler invoked when the notification is dismissed (by user or programmatically).
    /// </summary>
    internal Func<NotificationEventContext, Task>? DismissHandler { get; private set; }

    /// <summary>
    /// Handler invoked when the notification times out (hides from floating view).
    /// </summary>
    internal Func<NotificationEventContext, Task>? TimeoutHandler { get; private set; }

    /// <summary>
    /// Sets the timeout duration for this notification.
    /// After the timeout, the notification hides from floating view but remains in the notification panel.
    /// </summary>
    /// <param name="duration">The duration after which the notification hides from floating view.</param>
    /// <returns>This notification for fluent chaining.</returns>
    public Notification Timeout(TimeSpan duration)
    {
        TimeoutDuration = duration;
        return this;
    }

    /// <summary>
    /// Sets the primary action for this notification.
    /// </summary>
    /// <param name="label">The button label.</param>
    /// <param name="handler">The async handler invoked when the action is triggered.</param>
    /// <returns>This notification for fluent chaining.</returns>
    public Notification PrimaryAction(string label, Func<NotificationActionContext, Task> handler)
    {
        PrimaryActionValue = new NotificationAction(label, handler);
        return this;
    }

    /// <summary>
    /// Adds a secondary action to this notification.
    /// Secondary actions appear in a dropdown menu next to the primary action.
    /// </summary>
    /// <param name="label">The button label.</param>
    /// <param name="handler">The async handler invoked when the action is triggered.</param>
    /// <returns>This notification for fluent chaining.</returns>
    public Notification SecondaryAction(string label, Func<NotificationActionContext, Task> handler)
    {
        _secondaryActions.Add(new NotificationAction(label, handler));
        return this;
    }

    /// <summary>
    /// Sets the handler invoked when this notification is dismissed.
    /// </summary>
    /// <param name="handler">The async handler.</param>
    /// <returns>This notification for fluent chaining.</returns>
    public Notification OnDismiss(Func<NotificationEventContext, Task> handler)
    {
        DismissHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the handler invoked when this notification times out.
    /// Timeout hides the notification from floating view but keeps it in the panel.
    /// </summary>
    /// <param name="handler">The async handler.</param>
    /// <returns>This notification for fluent chaining.</returns>
    public Notification OnTimeout(Func<NotificationEventContext, Task> handler)
    {
        TimeoutHandler = handler;
        return this;
    }
}

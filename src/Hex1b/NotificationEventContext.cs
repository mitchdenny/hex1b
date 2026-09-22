using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Context passed to notification lifecycle event handlers (dismiss, timeout).
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to handlers registered with <see cref="Notification.OnDismiss"/>
/// and <see cref="Notification.OnTimeout"/>. It provides access to the notification itself
/// and the ability to dismiss it.
/// </para>
/// <para>
/// <strong>Difference from <see cref="NotificationActionContext"/>:</strong> This context
/// is for lifecycle events (dismiss/timeout), not action button clicks. It does not have
/// access to the input trigger since these events may be triggered programmatically.
/// </para>
/// </remarks>
/// <seealso cref="Notification"/>
/// <seealso cref="NotificationActionContext"/>
public class NotificationEventContext
{
    private readonly NotificationStack _stack;

    internal NotificationEventContext(
        Notification notification,
        NotificationStack stack,
        CancellationToken cancellationToken)
    {
        Notification = notification;
        _stack = stack;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The notification this event is for.
    /// </summary>
    public Notification Notification { get; }

    /// <summary>
    /// Cancellation token from the application run loop.
    /// </summary>
    /// <remarks>
    /// Use this token to cancel long-running operations when the app is shutting down.
    /// </remarks>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Dismisses this notification, removing it from the stack entirely.
    /// </summary>
    /// <remarks>
    /// After calling this, the notification will no longer appear in the drawer or as a floating card.
    /// </remarks>
    public void Dismiss() => _stack.Dismiss(Notification);
}

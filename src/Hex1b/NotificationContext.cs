using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Context passed to notification lifecycle event handlers (dismiss, timeout).
/// </summary>
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
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Dismisses this notification, removing it from the stack entirely.
    /// </summary>
    public void Dismiss() => _stack.Dismiss(Notification);
}

/// <summary>
/// Context passed to notification action handlers.
/// Extends <see cref="NotificationEventContext"/> with input trigger information.
/// </summary>
public class NotificationActionContext : NotificationEventContext
{
    internal NotificationActionContext(
        Notification notification,
        NotificationStack stack,
        CancellationToken cancellationToken,
        InputBindingActionContext inputTrigger)
        : base(notification, stack, cancellationToken)
    {
        InputTrigger = inputTrigger;
    }

    /// <summary>
    /// The input context that triggered this action (user clicked a button, pressed a key, etc.).
    /// Provides access to focus management, popups, clipboard, and other app-level services.
    /// </summary>
    public InputBindingActionContext InputTrigger { get; }
}

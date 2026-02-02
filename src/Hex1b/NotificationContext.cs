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

/// <summary>
/// Context passed to notification action button handlers.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to handlers registered with <see cref="Notification.PrimaryAction"/>
/// and <see cref="Notification.SecondaryAction"/>. It extends <see cref="NotificationEventContext"/>
/// with access to the input trigger.
/// </para>
/// <para>
/// Use <see cref="InputTrigger"/> to access app-level services like focus management, popups,
/// notifications, and clipboard - just like in regular button click handlers.
/// </para>
/// </remarks>
/// <example>
/// <para>A notification action that opens a file and dismisses the notification:</para>
/// <code>
/// new Notification("Download Complete", "file.zip downloaded")
///     .PrimaryAction("Open", async ctx =&gt; {
///         await OpenFileAsync("file.zip");
///         ctx.Dismiss();
///     })
/// </code>
/// </example>
/// <seealso cref="Notification"/>
/// <seealso cref="NotificationEventContext"/>
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
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides access to app-level services including:
    /// <list type="bullet">
    ///   <item><description><c>Notifications</c> - Post additional notifications</description></item>
    ///   <item><description><c>Focus</c> - Manage focus</description></item>
    ///   <item><description><c>Popups</c> - Show popups and dialogs</description></item>
    ///   <item><description><c>Clipboard</c> - Access clipboard</description></item>
    ///   <item><description><c>RequestStop()</c> - Request app termination</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public InputBindingActionContext InputTrigger { get; }
}

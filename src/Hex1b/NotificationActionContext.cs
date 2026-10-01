using Hex1b.Input;

namespace Hex1b;

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

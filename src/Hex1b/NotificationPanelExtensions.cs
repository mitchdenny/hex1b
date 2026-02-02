using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating notification panel widgets in widget contexts.
/// </summary>
/// <seealso cref="NotificationPanelWidget"/>
/// <seealso cref="Notification"/>
/// <seealso cref="NotificationStack"/>
public static class NotificationPanelExtensions
{
    /// <summary>
    /// Creates a notification panel that displays floating notifications and a slide-out drawer.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="content">The main content to display (notifications overlay on top).</param>
    /// <returns>A notification panel widget that can be further configured.</returns>
    /// <remarks>
    /// <para>
    /// The notification panel wraps your main content and provides notification display capabilities.
    /// Notifications appear as floating cards in the top-right corner and can be viewed in a
    /// slide-out drawer (toggled with Alt+N or by clicking the notification icon).
    /// </para>
    /// <para>
    /// The panel uses the app-wide notification stack from the nearest <see cref="ZStackWidget"/> ancestor.
    /// Post notifications from any event handler using <c>e.Context.Notifications.Post(...)</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>A notification-enabled application:</para>
    /// <code>
    /// ctx.ZStack(z =&gt; [
    ///     z.VStack(v =&gt; [
    ///         v.NotificationPanel(
    ///             v.Button("Save").OnClick(e =&gt; {
    ///                 SaveFile();
    ///                 e.Context.Notifications.Post(
    ///                     new Notification("Saved!", "File saved successfully")
    ///                         .Timeout(TimeSpan.FromSeconds(3)));
    ///             })
    ///         ).Fill()
    ///     ])
    /// ])
    /// </code>
    /// </example>
    public static NotificationPanelWidget NotificationPanel<T>(
        this WidgetContext<T> context,
        Hex1bWidget content) where T : Hex1bWidget
    {
        return new NotificationPanelWidget().WithContent(content);
    }

    /// <summary>
    /// Creates a notification panel with a custom maximum number of floating notifications.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="content">The main content to display.</param>
    /// <param name="maxFloating">Maximum number of floating notifications visible at once.</param>
    /// <returns>A notification panel widget.</returns>
    /// <remarks>
    /// Additional notifications beyond <paramref name="maxFloating"/> are queued and become visible
    /// as earlier ones time out or are dismissed. All notifications are always visible in the drawer.
    /// </remarks>
    public static NotificationPanelWidget NotificationPanel<T>(
        this WidgetContext<T> context,
        Hex1bWidget content,
        int maxFloating) where T : Hex1bWidget
    {
        return new NotificationPanelWidget()
            .WithContent(content)
            .WithMaxFloating(maxFloating);
    }
}

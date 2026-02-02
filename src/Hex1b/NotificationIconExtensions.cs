using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating notification icon widgets in widget contexts.
/// </summary>
/// <seealso cref="NotificationIconWidget"/>
/// <seealso cref="NotificationPanelWidget"/>
public static class NotificationIconExtensions
{
    /// <summary>
    /// Creates a notification bell icon that shows the unread count and toggles the notification drawer.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <returns>A notification icon widget.</returns>
    /// <remarks>
    /// <para>
    /// The notification icon must be placed inside a <see cref="NotificationPanelWidget"/> subtree
    /// to function correctly. It automatically discovers the parent panel's notification stack.
    /// </para>
    /// <para>
    /// The icon displays a bell (🔔) followed by the notification count. Clicking or pressing
    /// Enter toggles the notification drawer. Alt+N also toggles the drawer from anywhere.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>A menu bar with notification icon:</para>
    /// <code>
    /// ctx.HStack(bar =&gt; [
    ///     bar.Button("File"),
    ///     bar.Button("Edit"),
    ///     bar.Text("").FillWidth(),
    ///     bar.NotificationIcon()
    /// ])
    /// </code>
    /// </example>
    public static NotificationIconWidget NotificationIcon<T>(
        this WidgetContext<T> context) where T : Hex1bWidget
    {
        return new NotificationIconWidget();
    }

    /// <summary>
    /// Creates a notification icon with a custom bell character.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="bell">The character or string to use as the bell icon (e.g., "🔔", "␇", "*").</param>
    /// <returns>A notification icon widget with the custom bell character.</returns>
    /// <remarks>
    /// Use this overload when your terminal doesn't support emoji or you prefer a different icon.
    /// </remarks>
    public static NotificationIconWidget NotificationIcon<T>(
        this WidgetContext<T> context,
        string bell) where T : Hex1bWidget
    {
        return new NotificationIconWidget().WithBell(bell);
    }
}

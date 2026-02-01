using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating notification panel widgets.
/// </summary>
public static class NotificationPanelExtensions
{
    /// <summary>
    /// Creates a notification panel that hosts notifications for all descendant widgets.
    /// Notifications can be posted from any event handler using <c>e.Notifications.Post(...)</c>.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="content">The content to display (notifications overlay on top).</param>
    /// <returns>A notification panel widget.</returns>
    /// <example>
    /// <code>
    /// ctx.NotificationPanel(
    ///     ctx.VStack(v => [
    ///         v.Button("Save").OnClick(e => {
    ///             SaveFile();
    ///             e.Notifications.Post(new Notification("Saved!", "File saved successfully")
    ///                 .WithTimeout(TimeSpan.FromSeconds(3)));
    ///         })
    ///     ])
    /// )
    /// </code>
    /// </example>
    public static NotificationPanelWidget NotificationPanel<T>(
        this WidgetContext<T> context,
        Hex1bWidget content) where T : Hex1bWidget
    {
        return new NotificationPanelWidget().WithContent(content);
    }

    /// <summary>
    /// Creates a notification panel with an external notification stack.
    /// This allows the calling code to access notification state (count, visibility).
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="stack">The external notification stack to use.</param>
    /// <param name="content">The content to display.</param>
    /// <returns>A notification panel widget.</returns>
    /// <example>
    /// <code>
    /// var notifications = new NotificationStack();
    /// 
    /// ctx.NotificationPanel(notifications,
    ///     ctx.VStack(v => [
    ///         v.Text($"Notifications: {notifications.Count}"),
    ///         v.Button("Toggle Panel").OnClick(_ => notifications.TogglePanel())
    ///     ])
    /// )
    /// </code>
    /// </example>
    public static NotificationPanelWidget NotificationPanel<T>(
        this WidgetContext<T> context,
        NotificationStack stack,
        Hex1bWidget content) where T : Hex1bWidget
    {
        return new NotificationPanelWidget()
            .WithStack(stack)
            .WithContent(content);
    }

    /// <summary>
    /// Creates a notification panel with configuration options.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="content">The content to display.</param>
    /// <param name="maxFloating">Maximum floating notifications to show at once.</param>
    /// <returns>A notification panel widget.</returns>
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

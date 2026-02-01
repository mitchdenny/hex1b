using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating notification icon widgets.
/// </summary>
public static class NotificationIconExtensions
{
    /// <summary>
    /// Creates a notification bell icon that shows the count and toggles the panel.
    /// Must be placed inside a NotificationPanel.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <returns>A notification icon widget.</returns>
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
    /// <param name="bell">The bell character to display.</param>
    /// <returns>A notification icon widget.</returns>
    public static NotificationIconWidget NotificationIcon<T>(
        this WidgetContext<T> context,
        string bell) where T : Hex1bWidget
    {
        return new NotificationIconWidget().WithBell(bell);
    }
}

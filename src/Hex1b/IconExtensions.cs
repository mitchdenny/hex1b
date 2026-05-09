using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating IconWidget.
/// </summary>
public static class IconExtensions
{
    /// <summary>
    /// Creates an icon widget with the specified icon string.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="icon">The icon character or string to display.</param>
    /// <returns>A new IconWidget.</returns>
    /// <example>
    /// <code>
    /// context.Icon("▶")
    /// context.Icon("✓")
    /// context.Icon("🔍")
    /// </code>
    /// </example>
    public static IconWidget Icon<TParent>(this WidgetContext<TParent> context, string icon)
        where TParent : Hex1bWidget
        => new(icon);
    
    /// <summary>
    /// Creates an icon widget with the specified icon character.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="icon">The icon character to display.</param>
    /// <returns>A new IconWidget.</returns>
    public static IconWidget Icon<TParent>(this WidgetContext<TParent> context, char icon)
        where TParent : Hex1bWidget
        => new(icon.ToString());
}

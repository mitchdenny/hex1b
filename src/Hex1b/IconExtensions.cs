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
    /// <param name="ctx">The widget context.</param>
    /// <param name="icon">The icon character or string to display.</param>
    /// <returns>A new IconWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.Icon("▶")
    /// ctx.Icon("✓")
    /// ctx.Icon("🔍")
    /// </code>
    /// </example>
    public static IconWidget Icon<TParent>(this WidgetContext<TParent> ctx, string icon)
        where TParent : Hex1bWidget
        => new(icon);
    
    /// <summary>
    /// Creates an icon widget with the specified icon character.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="icon">The icon character to display.</param>
    /// <returns>A new IconWidget.</returns>
    public static IconWidget Icon<TParent>(this WidgetContext<TParent> ctx, char icon)
        where TParent : Hex1bWidget
        => new(icon.ToString());
}

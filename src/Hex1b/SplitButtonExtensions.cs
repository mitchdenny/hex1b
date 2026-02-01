using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating split button widgets.
/// </summary>
public static class SplitButtonExtensions
{
    /// <summary>
    /// Creates a split button with a primary action and optional secondary actions in a dropdown.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="primaryLabel">The label for the primary action.</param>
    /// <returns>A split button widget.</returns>
    /// <example>
    /// <code>
    /// ctx.SplitButton("Save")
    ///    .OnPrimaryClick(e => SaveFile())
    ///    .WithSecondaryAction("Save As...", e => SaveAs())
    ///    .WithSecondaryAction("Save All", e => SaveAll())
    /// </code>
    /// </example>
    public static SplitButtonWidget SplitButton<T>(
        this WidgetContext<T> context,
        string primaryLabel) where T : Hex1bWidget
    {
        return new SplitButtonWidget(primaryLabel);
    }
}

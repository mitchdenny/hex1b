using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating split button widgets in widget contexts.
/// </summary>
/// <seealso cref="SplitButtonWidget"/>
public static class SplitButtonExtensions
{
    /// <summary>
    /// Creates a split button with a primary action and optional secondary actions in a dropdown menu.
    /// </summary>
    /// <typeparam name="T">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <returns>A split button widget that can be further configured with primary and secondary actions.</returns>
    /// <remarks>
    /// <para>
    /// The split button renders as <c>[ Label ▼ ]</c> where clicking the label triggers the primary
    /// action and clicking the arrow opens a dropdown menu with secondary actions.
    /// </para>
    /// <para>
    /// Use <see cref="SplitButtonWidget.PrimaryAction(string, System.Action{Hex1b.Events.SplitButtonClickedEventArgs})"/>
    /// to set the primary action and <see cref="SplitButtonWidget.SecondaryAction(string, System.Action{Hex1b.Events.SplitButtonClickedEventArgs})"/>
    /// to add dropdown menu items.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>A save button with alternative save options:</para>
    /// <code>
    /// ctx.SplitButton()
    ///    .PrimaryAction("Save", e =&gt; SaveFile())
    ///    .SecondaryAction("Save As...", e =&gt; SaveAs())
    ///    .SecondaryAction("Save All", e =&gt; SaveAll())
    /// </code>
    /// </example>
    public static SplitButtonWidget SplitButton<T>(
        this WidgetContext<T> context) where T : Hex1bWidget
    {
        return new SplitButtonWidget();
    }
}

using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="InputOverrideWidget"/>.
/// </summary>
public static class InputOverrideExtensions
{
    /// <summary>
    /// Wraps a child widget in an <see cref="InputOverrideWidget"/> that allows
    /// centralized keybinding overrides for all descendant widgets of specified types.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.InputOverride(
    ///     ctx.VStack([list1, list2, textbox1])
    /// )
    /// .Override&lt;ListWidget&gt;(b =&gt;
    /// {
    ///     b.Remove(ListWidget.MoveUp);
    ///     b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
    /// });
    /// </code>
    /// </example>
    /// <typeparam name="TParent">The parent widget context type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="content">The child widget tree to wrap.</param>
    /// <returns>An <see cref="InputOverrideWidget"/> that can be further configured with <see cref="InputOverrideWidget.Override{TWidget}"/>.</returns>
    public static InputOverrideWidget InputOverride<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget content)
        where TParent : Hex1bWidget
        => new(content);
}

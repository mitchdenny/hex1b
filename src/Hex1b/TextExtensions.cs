namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating TextBlockWidget.
/// Returns the widget directly - covariance allows use in collection expressions.
/// </summary>
public static class TextExtensions
{
    /// <summary>
    /// Creates a TextBlockWidget with the specified text.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>
    /// Creates a TextBlockWidget with the specified text and overflow behavior.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string text,
        TextOverflow overflow)
        where TParent : Hex1bWidget
        => new(text, overflow);

    /// <summary>
    /// Creates a TextBlockWidget with text derived from state.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, string> textSelector)
        where TParent : Hex1bWidget
        => new(textSelector(ctx.State));

    /// <summary>
    /// Creates a TextBlockWidget with text derived from state and overflow behavior.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, string> textSelector,
        TextOverflow overflow)
        where TParent : Hex1bWidget
        => new(textSelector(ctx.State), overflow);
}

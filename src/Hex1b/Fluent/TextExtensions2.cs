namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating TextBlockWidget.
/// Returns the widget directly - covariance allows use in collection expressions.
/// </summary>
public static class TextExtensions2
{
    /// <summary>
    /// Creates a TextBlockWidget with the specified text.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>
    /// Creates a TextBlockWidget with text derived from state.
    /// </summary>
    public static TextBlockWidget Text<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, string> textSelector)
        where TParent : Hex1bWidget
        => new(textSelector(ctx.State));
}

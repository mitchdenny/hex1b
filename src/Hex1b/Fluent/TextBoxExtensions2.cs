namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building TextBoxWidget.
/// </summary>
public static class TextBoxExtensions2
{
    /// <summary>
    /// Creates a TextBox with the specified state.
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        TextBoxState textBoxState)
        where TParent : Hex1bWidget
        => new(textBoxState);

    /// <summary>
    /// Creates a TextBox with state selected from context state.
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, TextBoxState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));
}

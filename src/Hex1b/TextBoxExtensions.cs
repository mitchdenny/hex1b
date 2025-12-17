namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building TextBoxWidget.
/// </summary>
public static class TextBoxExtensions
{
    /// <summary>
    /// Creates a TextBox with internally managed state.
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a TextBox with the specified state (controlled mode).
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        TextBoxState textBoxState)
        where TParent : Hex1bWidget
        => new(textBoxState);

    /// <summary>
    /// Creates a TextBox with state selected from context state (controlled mode).
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, TextBoxState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));
}

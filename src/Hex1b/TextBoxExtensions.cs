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
    /// Creates a TextBox with initial text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string initialText)
        where TParent : Hex1bWidget
        => new(initialText);

    /// <summary>
    /// Creates a TextBox with explicit state (controlled mode).
    /// Use this when you need to read or manipulate the text box state externally.
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        TextBoxState state)
        where TParent : Hex1bWidget
        => new(State: state);

    /// <summary>
    /// Creates a TextBox with state selected from context state (controlled mode).
    /// </summary>
    public static TextBoxWidget TextBox<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, TextBoxState> stateSelector)
        where TParent : Hex1bWidget
        => new(State: stateSelector(ctx.State));
}

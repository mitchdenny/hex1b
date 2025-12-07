namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ButtonWidget.
/// </summary>
public static class ButtonExtensions2
{
    /// <summary>
    /// Creates a ButtonWidget with the specified label and click handler.
    /// </summary>
    public static ButtonWidget Button<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        string label,
        Action onClick)
        where TParent : Hex1bWidget
        => new(label, onClick);

    /// <summary>
    /// Creates a ButtonWidget with label derived from state.
    /// </summary>
    public static ButtonWidget Button<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, string> labelSelector,
        Action onClick)
        where TParent : Hex1bWidget
        => new(labelSelector(ctx.State), onClick);
}

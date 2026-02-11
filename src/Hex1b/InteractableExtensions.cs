namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="InteractableWidget"/>.
/// </summary>
public static class InteractableExtensions
{
    /// <summary>
    /// Creates an interactable wrapper around a single child widget.
    /// The builder lambda receives an <see cref="InteractableContext"/> with
    /// <see cref="InteractableContext.IsFocused"/> and <see cref="InteractableContext.IsHovered"/>
    /// properties that reflect the current interaction state.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Interactable(ic =>
    ///     ic.VStack(v => [
    ///         v.Text("Tile content"),
    ///     ])
    /// ).OnClick(args => DoSomething())
    /// </code>
    /// </example>
    public static InteractableWidget Interactable<TParent>(
        this WidgetContext<TParent> ctx,
        Func<InteractableContext, Hex1bWidget> builder)
        where TParent : Hex1bWidget
        => new(builder);

    /// <summary>
    /// Creates an interactable wrapper with an implicit VStack for multiple children.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Interactable(ic => [
    ///     ic.Text("Line 1"),
    ///     ic.Text("Line 2"),
    /// ]).OnClick(args => DoSomething())
    /// </code>
    /// </example>
    public static InteractableWidget Interactable<TParent>(
        this WidgetContext<TParent> ctx,
        Func<InteractableContext, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
        => new(ic => new VStackWidget(builder(ic)));
}

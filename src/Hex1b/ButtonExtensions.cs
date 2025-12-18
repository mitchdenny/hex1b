namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ButtonWidget.
/// </summary>
public static class ButtonExtensions
{
    /// <summary>
    /// Creates a ButtonWidget with the specified label and no action.
    /// </summary>
    public static ButtonWidget Button<TParent>(
        this WidgetContext<TParent> ctx,
        string label)
        where TParent : Hex1bWidget
        => new(label);

    /// <summary>
    /// Creates a ButtonWidget with the specified label and synchronous click handler.
    /// </summary>
    public static ButtonWidget Button<TParent>(
        this WidgetContext<TParent> ctx,
        string label,
        Action<ButtonClickedEventArgs> onClick)
        where TParent : Hex1bWidget
        => new(label) { OnClick = args => { onClick(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a ButtonWidget with the specified label and asynchronous click handler.
    /// </summary>
    public static ButtonWidget Button<TParent>(
        this WidgetContext<TParent> ctx,
        string label,
        Func<ButtonClickedEventArgs, Task> onClick)
        where TParent : Hex1bWidget
        => new(label) { OnClick = onClick };
}

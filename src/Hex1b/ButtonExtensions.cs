namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ButtonWidget.
/// </summary>
public static class ButtonExtensions
{
    /// <summary>
    /// Creates a ButtonWidget with the specified label.
    /// </summary>
    public static ButtonWidget Button<TParent>(
        this WidgetContext<TParent> context,
        string label)
        where TParent : Hex1bWidget
        => new(label);
}

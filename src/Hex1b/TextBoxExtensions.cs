namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building TextBoxWidget.
/// </summary>
public static class TextBoxExtensions
{
    /// <summary>
    /// Creates a TextBox with default empty text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> context)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a TextBox with the specified text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> context,
        string text)
        where TParent : Hex1bWidget
        => new(text);
}

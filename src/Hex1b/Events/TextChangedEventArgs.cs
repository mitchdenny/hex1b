using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for text box text change events.
/// </summary>
public sealed class TextChangedEventArgs : WidgetEventArgs<TextBoxWidget, TextBoxNode>
{
    /// <summary>
    /// The text content before the change.
    /// </summary>
    public string OldText { get; }

    /// <summary>
    /// The text content after the change.
    /// </summary>
    public string NewText { get; }

    public TextChangedEventArgs(
        TextBoxWidget widget,
        TextBoxNode node,
        InputBindingActionContext context,
        string oldText,
        string newText)
        : base(widget, node, context)
    {
        OldText = oldText;
        NewText = newText;
    }
}

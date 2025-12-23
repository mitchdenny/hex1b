using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for text box submit events (when Enter is pressed).
/// </summary>
public sealed class TextSubmittedEventArgs : WidgetEventArgs<TextBoxWidget, TextBoxNode>
{
    /// <summary>
    /// The text that was submitted.
    /// </summary>
    public string Text { get; }

    public TextSubmittedEventArgs(
        TextBoxWidget widget,
        TextBoxNode node,
        InputBindingActionContext context,
        string text)
        : base(widget, node, context)
    {
        Text = text;
    }
}

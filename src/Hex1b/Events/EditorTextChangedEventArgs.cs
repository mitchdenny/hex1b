using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for editor document text change events.
/// </summary>
public sealed class EditorTextChangedEventArgs : WidgetEventArgs<EditorWidget, EditorNode>
{
    public EditorTextChangedEventArgs(
        EditorWidget widget,
        EditorNode node,
        InputBindingActionContext context)
        : base(widget, node, context)
    {
    }
}

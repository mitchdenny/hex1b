using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for list selection change events.
/// </summary>
public sealed class ListSelectionChangedEventArgs : WidgetEventArgs<ListWidget, ListNode>
{
    /// <summary>
    /// The index of the newly selected item.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// The text of the newly selected item.
    /// </summary>
    public string SelectedText { get; }

    public ListSelectionChangedEventArgs(
        ListWidget widget,
        ListNode node,
        InputBindingActionContext context,
        int selectedIndex,
        string selectedText)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedText = selectedText;
    }
}

/// <summary>
/// Event arguments for list item activation events (Enter/Space key or click).
/// </summary>
public sealed class ListItemActivatedEventArgs : WidgetEventArgs<ListWidget, ListNode>
{
    /// <summary>
    /// The index of the activated item.
    /// </summary>
    public int ActivatedIndex { get; }

    /// <summary>
    /// The text of the activated item.
    /// </summary>
    public string ActivatedText { get; }

    public ListItemActivatedEventArgs(
        ListWidget widget,
        ListNode node,
        InputBindingActionContext context,
        int activatedIndex,
        string activatedText)
        : base(widget, node, context)
    {
        ActivatedIndex = activatedIndex;
        ActivatedText = activatedText;
    }
}

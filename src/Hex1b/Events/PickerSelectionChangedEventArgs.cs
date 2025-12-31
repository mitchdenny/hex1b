using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for picker selection changed events.
/// </summary>
public sealed class PickerSelectionChangedEventArgs : WidgetEventArgs<PickerWidget, PickerNode>
{
    /// <summary>
    /// The index of the newly selected item.
    /// </summary>
    public int SelectedIndex { get; }
    
    /// <summary>
    /// The text of the newly selected item.
    /// </summary>
    public string SelectedText { get; }

    internal PickerSelectionChangedEventArgs(
        PickerWidget widget, 
        PickerNode node, 
        InputBindingActionContext context,
        int selectedIndex,
        string selectedText)
        : base(widget, node, context)
    {
        SelectedIndex = selectedIndex;
        SelectedText = selectedText;
    }
}

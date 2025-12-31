using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for PickerWidget.
/// Manages the picker's selection state and popup display.
/// </summary>
public sealed class PickerNode : CompositeNode
{
    /// <summary>
    /// The list of items available for selection.
    /// </summary>
    public IReadOnlyList<string> Items { get; set; } = [];
    
    /// <summary>
    /// The index of the currently selected item.
    /// This value is owned by the node and preserved across reconciliation.
    /// </summary>
    public int SelectedIndex { get; set; }
    
    /// <summary>
    /// Tracks whether the initial selection has been applied.
    /// Once true, the InitialSelectedIndex from the widget is ignored.
    /// </summary>
    internal bool HasAppliedInitialSelection { get; set; }
    
    /// <summary>
    /// Gets the text of the currently selected item, or empty string if no items.
    /// </summary>
    public string SelectedText => Items.Count > 0 && SelectedIndex >= 0 && SelectedIndex < Items.Count
        ? Items[SelectedIndex]
        : "";
    
    /// <summary>
    /// Callback invoked when selection changes.
    /// </summary>
    public Func<InputBindingActionContext, Task>? SelectionChangedAction { get; set; }
    
    /// <summary>
    /// Reference to the action context for popup operations.
    /// </summary>
    internal InputBindingActionContext? CurrentContext { get; set; }
    
    /// <summary>
    /// Opens the picker popup with the next item pre-selected.
    /// Wraps around to the first item if at the last item.
    /// </summary>
    internal void OpenWithNextItem(InputBindingActionContext context, PickerWidget widget)
    {
        if (Items.Count == 0) return;
        
        var nextIndex = (SelectedIndex + 1) % Items.Count;
        OpenPopupWithSelection(context, widget, nextIndex);
    }
    
    /// <summary>
    /// Opens the picker popup with the previous item pre-selected.
    /// Wraps around to the last item if at the first item.
    /// </summary>
    internal void OpenWithPreviousItem(InputBindingActionContext context, PickerWidget widget)
    {
        if (Items.Count == 0) return;
        
        var prevIndex = SelectedIndex == 0 ? Items.Count - 1 : SelectedIndex - 1;
        OpenPopupWithSelection(context, widget, prevIndex);
    }
    
    /// <summary>
    /// Opens the popup with a specific item pre-selected.
    /// </summary>
    private void OpenPopupWithSelection(InputBindingActionContext context, PickerWidget widget, int selectedIndex)
    {
        // Get the button node (content child) to use as anchor and focus restore target
        var buttonNode = ContentChild;
        if (buttonNode == null) return;
        
        // Build and show popup list with the specified selection
        var list = new ListWidget(Items) { InitialSelectedIndex = selectedIndex }
            .OnItemActivated(async e =>
            {
                await SelectItemAsync(e.ActivatedIndex, e.Context);
            });
        
        // Wrap in a border for visual distinction
        var popupContent = new BorderWidget(list);
        
        // Push the popup anchored to the button, with focus restore to the button
        context.Popups.PushAnchored(buttonNode, AnchorPosition.Below, popupContent, context.FocusedNode);
    }
    
    /// <summary>
    /// Selects an item by index and dismisses the popup.
    /// </summary>
    public async Task SelectItemAsync(int index, InputBindingActionContext context)
    {
        if (index < 0 || index >= Items.Count)
        {
            return;
        }
        
        var oldIndex = SelectedIndex;
        SelectedIndex = index;
        
        // Dismiss the popup and restore focus to the node that was focused when it was opened.
        // We set IsFocused directly on the restore node because the FocusRing currently only
        // contains the popup's focusables (ZStack returns topmost layer only). After the popup
        // is removed and the FocusRing is rebuilt, EnsureFocus will see this node is already
        // focused and won't override it.
        if (context.Popups.Pop(out var focusRestoreNode) && focusRestoreNode != null)
        {
            // Clear focus on the currently focused node (the popup's list)
            var currentlyFocused = context.FocusedNode;
            if (currentlyFocused != null)
            {
                currentlyFocused.IsFocused = false;
            }
            
            // Set focus on the restore node
            focusRestoreNode.IsFocused = true;
        }
        
        // Notify if selection actually changed
        if (oldIndex != index && SelectionChangedAction != null)
        {
            await SelectionChangedAction(context);
        }
        
        MarkDirty();
    }
}

using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class ListNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public ListWidget? SourceWidget { get; set; }

    /// <summary>
    /// The list items to display.
    /// </summary>
    public IReadOnlyList<string> Items { get; set; } = [];

    /// <summary>
    /// The currently selected index. This is preserved across reconciliation.
    /// </summary>
    public int SelectedIndex { get; set; } = 0;

    /// <summary>
    /// The text of the currently selected item, or null if the list is empty.
    /// </summary>
    public string? SelectedText => SelectedIndex >= 0 && SelectedIndex < Items.Count 
        ? Items[SelectedIndex] 
        : null;

    /// <summary>
    /// Internal action invoked when selection changes.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SelectionChangedAction { get; set; }

    /// <summary>
    /// Internal action invoked when an item is activated.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? ItemActivatedAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    private bool _isHovered;
    public override bool IsHovered { get => _isHovered; set => _isHovered = value; }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation with selection changed events
        bindings.Key(Hex1bKey.UpArrow).Action(MoveUpWithEvent, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveDownWithEvent, "Move down");
        
        // Activation - always bind Enter/Space so focused lists consume these keys
        bindings.Key(Hex1bKey.Enter).Action(ActivateItemWithEvent, "Activate item");
        bindings.Key(Hex1bKey.Spacebar).Action(ActivateItemWithEvent, "Activate item");
        
        // Mouse click to select and activate
        bindings.Mouse(MouseButton.Left).Action(MouseSelectAndActivate, "Select and activate item");
    }

    private async Task MouseSelectAndActivate(InputBindingActionContext ctx)
    {
        // The mouse position is available in the context, calculate local Y to determine which item was clicked
        var localY = ctx.MouseY - Bounds.Y;
        if (localY >= 0 && localY < Items.Count)
        {
            SetSelection(localY);
            if (ItemActivatedAction != null)
            {
                await ItemActivatedAction(ctx);
            }
        }
    }

    private async Task ActivateItemWithEvent(InputBindingActionContext ctx)
    {
        if (ItemActivatedAction != null)
        {
            await ItemActivatedAction(ctx);
        }
    }

    private async Task MoveUpWithEvent(InputBindingActionContext ctx)
    {
        MoveUp();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    private async Task MoveDownWithEvent(InputBindingActionContext ctx)
    {
        MoveDown();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    /// <summary>
    /// Moves selection up (with wrap-around).
    /// </summary>
    internal void MoveUp()
    {
        if (Items.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Items.Count - 1 : SelectedIndex - 1;
    }

    /// <summary>
    /// Moves selection down (with wrap-around).
    /// </summary>
    internal void MoveDown()
    {
        if (Items.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Items.Count;
    }

    /// <summary>
    /// Sets the selection to a specific index.
    /// </summary>
    internal void SetSelection(int index)
    {
        if (Items.Count == 0 || index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    /// <summary>
    /// Handles mouse click by selecting the item at the clicked row.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        if (localY >= 0 && localY < Items.Count)
        {
            SetSelection(localY);
            return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }

    public override Size Measure(Constraints constraints)
    {
        // List: width is max item length + indicator (2 chars), height is item count
        var maxWidth = 0;
        foreach (var item in Items)
        {
            maxWidth = Math.Max(maxWidth, item.Length + 2); // "> " indicator
        }
        var height = Math.Max(Items.Count, 1);
        return constraints.Constrain(new Size(maxWidth, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        
        // Get inherited colors for non-selected items
        var inheritedColors = context.GetInheritedColorCodes();
        var resetToInherited = context.GetResetToInheritedCodes();
        
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var isSelected = i == SelectedIndex;

            var x = Bounds.X;
            var y = Bounds.Y + i;
            
            string text;
            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                text = $"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item}{resetToInherited}";
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator with inherited colors
                text = $"{inheritedColors}{selectedIndicator}{item}{resetToInherited}";
            }
            else
            {
                // Not selected: use inherited colors
                text = $"{inheritedColors}{unselectedIndicator}{item}{resetToInherited}";
            }

            // Use clipped rendering when a layout provider is active
            if (context.CurrentLayoutProvider != null)
            {
                context.WriteClipped(x, y, text);
            }
            else
            {
                context.SetCursorPosition(x, y);
                context.Write(text);
            }
        }
    }
}

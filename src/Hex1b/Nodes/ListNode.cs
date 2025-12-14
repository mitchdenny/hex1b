using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class ListNode : Hex1bNode
{
    public ListState State { get; set; } = new();
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    public override bool IsFocusable => true;

    public override Size Measure(Constraints constraints)
    {
        // List: width is max item length + indicator (2 chars), height is item count
        var items = State.Items;
        var maxWidth = 0;
        foreach (var item in items)
        {
            maxWidth = Math.Max(maxWidth, item.Text.Length + 2); // "> " indicator
        }
        var height = Math.Max(items.Count, 1);
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
        
        var items = State.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isSelected = i == State.SelectedIndex;

            var x = Bounds.X;
            var y = Bounds.Y + i;
            
            string text;
            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                text = $"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item.Text}{resetToInherited}";
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator with inherited colors
                text = $"{inheritedColors}{selectedIndicator}{item.Text}{resetToInherited}";
            }
            else
            {
                // Not selected: use inherited colors
                text = $"{inheritedColors}{unselectedIndicator}{item.Text}{resetToInherited}";
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

    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        if (!IsFocused) return InputResult.NotHandled;

        switch (keyEvent.Key)
        {
            case Hex1bKey.UpArrow:
                State.MoveUp();
                return InputResult.Handled;
            case Hex1bKey.DownArrow:
                State.MoveDown();
                return InputResult.Handled;
            case Hex1bKey.Enter:
            case Hex1bKey.Spacebar:
                // Trigger item activated on Enter or Space
                if (State.SelectedItem != null)
                {
                    State.OnItemActivated?.Invoke(State.SelectedItem);
                }
                return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }
}

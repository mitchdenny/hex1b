using Custard.Layout;
using Custard.Theming;
using Custard.Widgets;

namespace Custard;

public sealed class ListNode : CustardNode
{
    public ListState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

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

    public override void Render(CustardRenderContext context)
    {
        var theme = context.Theme;
        var selectedIndicator = theme.Get(ListTheme.SelectedIndicator);
        var unselectedIndicator = theme.Get(ListTheme.UnselectedIndicator);
        var selectedFg = theme.Get(ListTheme.SelectedForegroundColor);
        var selectedBg = theme.Get(ListTheme.SelectedBackgroundColor);
        
        var items = State.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isSelected = i == State.SelectedIndex;

            if (isSelected && IsFocused)
            {
                // Focused and selected: use theme colors
                context.Write($"{selectedFg.ToForegroundAnsi()}{selectedBg.ToBackgroundAnsi()}{selectedIndicator}{item.Text}\x1b[0m");
            }
            else if (isSelected)
            {
                // Selected but not focused: just show indicator
                context.Write($"{selectedIndicator}{item.Text}");
            }
            else
            {
                // Not selected
                context.Write($"{unselectedIndicator}{item.Text}");
            }

            if (i < items.Count - 1)
            {
                context.Write("\n");
            }
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        if (!IsFocused) return false;

        if (evt is KeyInputEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case ConsoleKey.UpArrow:
                    State.MoveUp();
                    return true;
                case ConsoleKey.DownArrow:
                    State.MoveDown();
                    return true;
                case ConsoleKey.Enter:
                    // Trigger selection changed on Enter as well
                    if (State.SelectedItem != null)
                    {
                        State.OnSelectionChanged?.Invoke(State.SelectedItem);
                    }
                    return true;
            }
        }
        return false;
    }
}

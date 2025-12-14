using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    public TextBoxState State { get; set; } = new();
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation
        bindings.Key(Hex1bKey.LeftArrow).Action(MoveLeft, "Move left");
        bindings.Key(Hex1bKey.RightArrow).Action(MoveRight, "Move right");
        bindings.Key(Hex1bKey.Home).Action(MoveHome, "Go to start");
        bindings.Key(Hex1bKey.End).Action(MoveEnd, "Go to end");
        
        // Selection navigation
        bindings.Shift().Key(Hex1bKey.LeftArrow).Action(SelectLeft, "Extend selection left");
        bindings.Shift().Key(Hex1bKey.RightArrow).Action(SelectRight, "Extend selection right");
        bindings.Shift().Key(Hex1bKey.Home).Action(SelectToStart, "Select to start");
        bindings.Shift().Key(Hex1bKey.End).Action(SelectToEnd, "Select to end");
        
        // Editing
        bindings.Key(Hex1bKey.Backspace).Action(DeleteBackward, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Action(DeleteForward, "Delete forward");
        
        // Selection
        bindings.Ctrl().Key(Hex1bKey.A).Action(SelectAll, "Select all");
        
        // Character input - matches any printable text (including emojis)
        bindings.AnyCharacter().Action(InsertText, "Type text");
    }

    private void InsertText(string text)
    {
        // If there's a selection, delete it first
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        State.Text = State.Text.Insert(State.CursorPosition, text);
        State.CursorPosition += text.Length;
    }

    private void MoveLeft()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionStart;
            State.ClearSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
    }

    private void MoveRight()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionEnd;
            State.ClearSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
    }

    private void MoveHome()
    {
        State.ClearSelection();
        State.CursorPosition = 0;
    }

    private void MoveEnd()
    {
        State.ClearSelection();
        State.CursorPosition = State.Text.Length;
    }

    private void SelectLeft()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
    }

    private void SelectRight()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
    }

    private void SelectToStart()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = 0;
    }

    private void SelectToEnd()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = State.Text.Length;
    }

    private void DeleteBackward()
    {
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterStart = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = State.CursorPosition - clusterStart;
            State.Text = State.Text.Remove(clusterStart, clusterLength);
            State.CursorPosition = clusterStart;
        }
    }

    private void DeleteForward()
    {
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterEnd = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = clusterEnd - State.CursorPosition;
            State.Text = State.Text.Remove(State.CursorPosition, clusterLength);
        }
    }

    private void DeleteSelection()
    {
        if (!State.HasSelection) return;
        var start = State.SelectionStart;
        var end = State.SelectionEnd;
        State.Text = State.Text[..start] + State.Text[end..];
        State.CursorPosition = start;
        State.ClearSelection();
    }

    private void SelectAll() => State.SelectAll();

    public override Size Measure(Constraints constraints)
    {
        // TextBox renders as "[text]" - 2 chars for brackets + text display width (or at least 1 for cursor)
        // Use display width to account for wide characters (emoji, CJK)
        var textDisplayWidth = Math.Max(DisplayWidth.GetStringWidth(State.Text), 1);
        var width = textDisplayWidth + 2; // +2 for brackets
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(TextBoxTheme.LeftBracket);
        var rightBracket = theme.Get(TextBoxTheme.RightBracket);
        var cursorFg = theme.Get(TextBoxTheme.CursorForegroundColor);
        var cursorBg = theme.Get(TextBoxTheme.CursorBackgroundColor);
        var selFg = theme.Get(TextBoxTheme.SelectionForegroundColor);
        var selBg = theme.Get(TextBoxTheme.SelectionBackgroundColor);
        
        var text = State.Text;
        var cursor = State.CursorPosition;
        var inheritedColors = context.GetInheritedColorCodes();
        var resetToInherited = context.GetResetToInheritedCodes();

        string output;
        if (IsFocused)
        {
            if (State.HasSelection)
            {
                // Render with selection highlight
                var selStart = State.SelectionStart;
                var selEnd = State.SelectionEnd;
                
                var beforeSel = text[..selStart];
                var selected = text[selStart..selEnd];
                var afterSel = text[selEnd..];
                
                // Use theme colors for selection, inherit for rest
                output = $"{inheritedColors}{leftBracket}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToInherited}{afterSel}{rightBracket}";
            }
            else
            {
                // Show text with cursor as themed block
                // Get the entire grapheme cluster at cursor position for proper rendering
                var before = text[..cursor];
                string cursorCluster;
                string after;
                if (cursor < text.Length)
                {
                    var clusterLength = GraphemeHelper.GetClusterLength(text, cursor);
                    cursorCluster = text.Substring(cursor, clusterLength);
                    after = text[(cursor + clusterLength)..];
                }
                else
                {
                    cursorCluster = " ";
                    after = "";
                }
                
                output = $"{inheritedColors}{leftBracket}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorCluster}{resetToInherited}{after}{rightBracket}";
            }
        }
        else
        {
            output = $"{inheritedColors}{leftBracket}{text}{rightBracket}{resetToInherited}";
        }
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}

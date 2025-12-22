using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public TextBoxWidget? SourceWidget { get; set; }

    /// <summary>
    /// The text box state containing text, cursor position, and selection.
    /// </summary>
    internal TextBoxState State { get; set; } = new();

    /// <summary>
    /// Tracks the last text value provided by the widget during reconciliation.
    /// Used to detect when external code changes the text vs internal user input.
    /// </summary>
    internal string? LastWidgetText { get; set; }
    
    /// <summary>
    /// Gets or sets the text content. Convenience property that accesses State.Text.
    /// </summary>
    public string Text
    {
        get => State.Text;
        set
        {
            if (State.Text != value)
            {
                State.Text = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Internal action invoked when text content changes.
    /// </summary>
    internal Func<InputBindingActionContext, string, string, Task>? TextChangedAction { get; set; }

    /// <summary>
    /// Internal action invoked when Enter is pressed.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SubmitAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    /// <summary>
    /// TextBox uses a blinking bar cursor to indicate text input position.
    /// </summary>
    public override CursorShape PreferredCursorShape => CursorShape.BlinkingBar;

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
        
        // Editing - use async handlers to fire callbacks
        bindings.Key(Hex1bKey.Backspace).Action(DeleteBackwardAsync, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Action(DeleteForwardAsync, "Delete forward");
        
        // Submit (Enter key)
        if (SubmitAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(SubmitAction, "Submit");
        }
        
        // Selection
        bindings.Ctrl().Key(Hex1bKey.A).Action(SelectAll, "Select all");
        
        // Mouse: double-click to select all
        bindings.Mouse(MouseButton.Left).DoubleClick().Action(SelectAll, "Select all");
        
        // Character input - matches any printable text (including emojis)
        bindings.AnyCharacter().Action(InsertTextAsync, "Type text");
    }

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        // If there's a selection, delete it first
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        State.Text = State.Text.Insert(State.CursorPosition, text);
        State.CursorPosition += text.Length;
        MarkDirty();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
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
        MarkDirty();
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
        MarkDirty();
    }

    private void MoveHome()
    {
        State.ClearSelection();
        State.CursorPosition = 0;
        MarkDirty();
    }

    private void MoveEnd()
    {
        State.ClearSelection();
        State.CursorPosition = State.Text.Length;
        MarkDirty();
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
        MarkDirty();
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
        MarkDirty();
    }

    private void SelectToStart()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = 0;
        MarkDirty();
    }

    private void SelectToEnd()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = State.Text.Length;
        MarkDirty();
    }

    private async Task DeleteBackwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
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
            MarkDirty();
        }
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteForwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
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
            MarkDirty();
        }
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
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
        MarkDirty();
    }

    private void SelectAll()
    {
        State.SelectAll();
        MarkDirty();
    }

    /// <summary>
    /// Handles mouse click to position the cursor within the text.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // Only handle left clicks that aren't double/triple clicks
        // (double-click is handled by the binding for SelectAll)
        if (mouseEvent.Button != MouseButton.Left || mouseEvent.ClickCount > 1)
        {
            return InputResult.NotHandled;
        }

        // The TextBox renders as "[text]" so localX=0 is '[', localX=1 is first char
        // Convert click position to text cursor position
        var textColumn = localX - 1; // Subtract 1 for the '[' bracket
        
        if (textColumn < 0)
        {
            // Clicked on or before the '[' - position at start
            State.ClearSelection();
            State.CursorPosition = 0;
        }
        else
        {
            // Find the cursor position that corresponds to this display column
            var cursorPos = DisplayColumnToTextPosition(textColumn);
            State.ClearSelection();
            State.CursorPosition = cursorPos;
        }

        return InputResult.Handled;
    }

    /// <summary>
    /// Converts a display column (0-based, relative to text start) to a text cursor position.
    /// Accounts for variable-width characters (wide chars, emoji, etc.).
    /// </summary>
    private int DisplayColumnToTextPosition(int displayColumn)
    {
        if (string.IsNullOrEmpty(State.Text))
            return 0;

        int currentColumn = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(State.Text);
        int lastPosition = 0;

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            var graphemeStart = enumerator.ElementIndex;

            // If the click is before or at the start of this grapheme
            if (displayColumn < currentColumn + graphemeWidth)
            {
                // Click is in the first half of the grapheme - position before it
                // Click is in the second half - position after it
                var midpoint = currentColumn + (graphemeWidth / 2.0);
                if (displayColumn < midpoint)
                {
                    return graphemeStart;
                }
                else
                {
                    return graphemeStart + grapheme.Length;
                }
            }

            currentColumn += graphemeWidth;
            lastPosition = graphemeStart + grapheme.Length;
        }

        // Click is past the end of the text
        return State.Text.Length;
    }

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
        var hoverCursorFg = theme.Get(TextBoxTheme.HoverCursorForegroundColor);
        var hoverCursorBg = theme.Get(TextBoxTheme.HoverCursorBackgroundColor);
        
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
        else if (IsHovered && context.MouseX >= 0 && context.MouseY >= 0)
        {
            // Show a hover cursor preview where clicking would position the cursor
            output = RenderWithHoverCursor(text, leftBracket, rightBracket, 
                inheritedColors, resetToInherited, hoverCursorFg, hoverCursorBg, context);
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
    
    /// <summary>
    /// Renders the text with a faint hover cursor showing where clicking would position the cursor.
    /// </summary>
    private string RenderWithHoverCursor(
        string text, 
        string leftBracket, 
        string rightBracket,
        string inheritedColors,
        string resetToInherited,
        Hex1bColor hoverCursorFg,
        Hex1bColor hoverCursorBg,
        Hex1bRenderContext context)
    {
        // Calculate local mouse position relative to this node
        var localMouseX = context.MouseX - Bounds.X;
        
        // Convert to text column (subtract 1 for '[' bracket)
        var textColumn = localMouseX - 1;
        
        // If mouse is outside the text area, show cursor at the nearest edge
        if (textColumn < 0)
        {
            // Mouse is on or before '[' - show cursor at start
            textColumn = 0;
        }
        
        // Find the cursor position and display column for the hover cursor
        var hoverCursorPos = DisplayColumnToTextPosition(textColumn);
        
        // Get the grapheme cluster at the hover position (or space if at end)
        string before = text[..hoverCursorPos];
        string hoverCluster;
        string after;
        
        if (hoverCursorPos < text.Length)
        {
            var clusterLength = GraphemeHelper.GetClusterLength(text, hoverCursorPos);
            hoverCluster = text.Substring(hoverCursorPos, clusterLength);
            after = text[(hoverCursorPos + clusterLength)..];
        }
        else
        {
            // At end of text - show a space as the hover target
            hoverCluster = " ";
            after = "";
        }
        
        // Build the hover cursor color codes
        var hoverColors = "";
        if (!hoverCursorFg.IsDefault) hoverColors += hoverCursorFg.ToForegroundAnsi();
        if (!hoverCursorBg.IsDefault) hoverColors += hoverCursorBg.ToBackgroundAnsi();
        
        return $"{inheritedColors}{leftBracket}{before}{hoverColors}{hoverCluster}{resetToInherited}{after}{rightBracket}";
    }
}

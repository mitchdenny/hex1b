using Custard.Layout;
using Custard.Theming;

namespace Custard;

public sealed class SplitterNode : CustardNode
{
    public CustardNode? Left { get; set; }
    public CustardNode? Right { get; set; }
    public int LeftWidth { get; set; } = 30;
    private int _focusedIndex = 0;
    private List<CustardNode>? _focusableNodes;

    public override Size Measure(Constraints constraints)
    {
        // Splitter: left width + divider (3 chars " │ ") + right content
        var leftSize = Left?.Measure(Constraints.Unbounded) ?? Size.Zero;
        var rightSize = Right?.Measure(Constraints.Unbounded) ?? Size.Zero;
        
        var width = LeftWidth + 3 + rightSize.Width;
        var height = Math.Max(leftSize.Height, rightSize.Height);
        
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Left pane gets LeftWidth
        if (Left != null)
        {
            Left.Arrange(new Rect(bounds.X, bounds.Y, LeftWidth, bounds.Height));
        }
        
        // Right pane gets remaining width (minus 3 for divider)
        if (Right != null)
        {
            var rightX = bounds.X + LeftWidth + 3;
            var rightWidth = Math.Max(0, bounds.Width - LeftWidth - 3);
            Right.Arrange(new Rect(rightX, bounds.Y, rightWidth, bounds.Height));
        }
    }

    public override IEnumerable<CustardNode> GetFocusableNodes()
    {
        if (Left != null)
        {
            foreach (var focusable in Left.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
        if (Right != null)
        {
            foreach (var focusable in Right.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    private List<CustardNode> GetFocusableNodesList()
    {
        _focusableNodes ??= GetFocusableNodes().ToList();
        return _focusableNodes;
    }

    public void InvalidateFocusCache()
    {
        _focusableNodes = null;
    }

    public void SetInitialFocus()
    {
        var focusables = GetFocusableNodesList();
        if (focusables.Count > 0)
        {
            SetNodeFocus(focusables[0], true);
        }
    }

    public override void Render(CustardRenderContext context)
    {
        var theme = context.Theme;
        var dividerChar = theme.Get(SplitterTheme.DividerCharacter);
        var dividerColor = theme.Get(SplitterTheme.DividerColor);
        
        // Get the rendered content of left and right as lines
        var leftLines = RenderToLines(Left, theme);
        var rightLines = RenderToLines(Right, theme);

        // Ensure both have the same number of lines
        var maxLines = Math.Max(leftLines.Count, rightLines.Count);
        while (leftLines.Count < maxLines) leftLines.Add("");
        while (rightLines.Count < maxLines) rightLines.Add("");

        // Render side by side with a vertical bar separator
        for (int i = 0; i < maxLines; i++)
        {
            var leftText = leftLines[i];
            var visibleLength = GetVisibleLength(leftText);
            
            // Truncate if too long (need to be careful with ANSI codes)
            if (visibleLength > LeftWidth)
            {
                leftText = TruncateToVisibleWidth(leftText, LeftWidth);
                visibleLength = LeftWidth;
            }
            
            // Pad to reach LeftWidth
            var padding = LeftWidth - visibleLength;
            
            context.Write(leftText);
            context.Write(new string(' ', padding));
            context.Write($" {dividerColor.ToForegroundAnsi()}{dividerChar}\x1b[0m ");
            context.Write(rightLines[i]);

            if (i < maxLines - 1)
            {
                context.Write("\n");
            }
        }
    }

    /// <summary>
    /// Gets the visible length of a string, ignoring ANSI escape sequences.
    /// </summary>
    private static int GetVisibleLength(string text)
    {
        var length = 0;
        var inEscape = false;
        foreach (var c in text)
        {
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape)
            {
                // End of escape sequence is a letter
                if (char.IsLetter(c))
                {
                    inEscape = false;
                }
            }
            else
            {
                length++;
            }
        }
        return length;
    }

    /// <summary>
    /// Truncates a string to a visible width, preserving ANSI escape sequences.
    /// </summary>
    private static string TruncateToVisibleWidth(string text, int maxWidth)
    {
        var result = new System.Text.StringBuilder();
        var visibleCount = 0;
        var inEscape = false;
        
        foreach (var c in text)
        {
            if (c == '\x1b')
            {
                inEscape = true;
                result.Append(c);
            }
            else if (inEscape)
            {
                result.Append(c);
                if (char.IsLetter(c))
                {
                    inEscape = false;
                }
            }
            else
            {
                if (visibleCount < maxWidth)
                {
                    result.Append(c);
                    visibleCount++;
                }
            }
        }
        
        // Reset any formatting at the end
        result.Append("\x1b[0m");
        return result.ToString();
    }

    private static List<string> RenderToLines(CustardNode? node, Theming.CustardTheme theme)
    {
        if (node == null) return [""];
        
        var buffer = new StringRenderBuffer();
        var tempContext = new CustardRenderContext(buffer, theme);
        node.Render(tempContext);
        return buffer.GetLines();
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        // Handle Tab to move focus across all focusable nodes
        if (evt is KeyInputEvent keyEvent && keyEvent.Key == ConsoleKey.Tab)
        {
            var focusables = GetFocusableNodesList();
            if (focusables.Count > 0)
            {
                // Clear old focus
                if (_focusedIndex >= 0 && _focusedIndex < focusables.Count)
                {
                    SetNodeFocus(focusables[_focusedIndex], false);
                }

                // Move focus
                if (keyEvent.Shift)
                {
                    _focusedIndex = _focusedIndex <= 0 ? focusables.Count - 1 : _focusedIndex - 1;
                }
                else
                {
                    _focusedIndex = (_focusedIndex + 1) % focusables.Count;
                }

                // Set new focus
                SetNodeFocus(focusables[_focusedIndex], true);
                return true;
            }
        }

        // Dispatch to focused node
        var focusablesList = GetFocusableNodesList();
        if (_focusedIndex >= 0 && _focusedIndex < focusablesList.Count)
        {
            return focusablesList[_focusedIndex].HandleInput(evt);
        }

        return false;
    }

    private static void SetNodeFocus(CustardNode node, bool focused)
    {
        switch (node)
        {
            case TextBoxNode textBox:
                textBox.IsFocused = focused;
                break;
            case ButtonNode button:
                button.IsFocused = focused;
                break;
            case ListNode list:
                list.IsFocused = focused;
                break;
        }
    }
}

/// <summary>
/// A simple string buffer that implements ICustardTerminalOutput for off-screen rendering.
/// </summary>
internal class StringRenderBuffer : ICustardTerminalOutput
{
    private readonly System.Text.StringBuilder _buffer = new();

    public int Width => 200;
    public int Height => 50;

    public void Write(string text) => _buffer.Append(text);
    public void Clear() => _buffer.Clear();
    public void SetCursorPosition(int left, int top) { }
    public void EnterAlternateScreen() { }
    public void ExitAlternateScreen() { }

    public List<string> GetLines()
    {
        var text = _buffer.ToString();
        // Split by newlines, keeping ANSI codes intact
        return text.Split('\n').ToList();
    }
}

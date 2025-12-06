namespace Custard.Widgets;

/// <summary>
/// Holds the mutable state for a TextBox. Create once and reuse across renders.
/// </summary>
public class TextBoxState
{
    public string Text { get; set; } = "";
    public int CursorPosition { get; set; } = 0;
    
    /// <summary>
    /// The anchor position for text selection. If null, no selection is active.
    /// Selection range is from min(SelectionAnchor, CursorPosition) to max(SelectionAnchor, CursorPosition).
    /// </summary>
    public int? SelectionAnchor { get; set; } = null;

    /// <summary>
    /// Returns true if there is an active text selection.
    /// </summary>
    public bool HasSelection => SelectionAnchor.HasValue && SelectionAnchor.Value != CursorPosition;

    /// <summary>
    /// Gets the start index of the selection (inclusive).
    /// </summary>
    public int SelectionStart => HasSelection ? Math.Min(SelectionAnchor!.Value, CursorPosition) : CursorPosition;

    /// <summary>
    /// Gets the end index of the selection (exclusive).
    /// </summary>
    public int SelectionEnd => HasSelection ? Math.Max(SelectionAnchor!.Value, CursorPosition) : CursorPosition;

    /// <summary>
    /// Gets the selected text.
    /// </summary>
    public string SelectedText => HasSelection ? Text[SelectionStart..SelectionEnd] : "";

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        SelectionAnchor = null;
    }

    /// <summary>
    /// Deletes the selected text and returns cursor to selection start.
    /// </summary>
    private void DeleteSelection()
    {
        if (!HasSelection) return;
        
        var start = SelectionStart;
        var end = SelectionEnd;
        Text = Text[..start] + Text[end..];
        CursorPosition = start;
        ClearSelection();
    }

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll()
    {
        if (Text.Length == 0) return;
        SelectionAnchor = 0;
        CursorPosition = Text.Length;
    }

    public void HandleInput(KeyInputEvent evt)
    {
        // Ctrl+A: Select all
        if (evt.Control && evt.Key == ConsoleKey.A)
        {
            SelectAll();
            return;
        }

        switch (evt.Key)
        {
            case ConsoleKey.Backspace:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (CursorPosition > 0)
                {
                    Text = Text.Remove(CursorPosition - 1, 1);
                    CursorPosition--;
                }
                break;

            case ConsoleKey.Delete:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (CursorPosition < Text.Length)
                {
                    Text = Text.Remove(CursorPosition, 1);
                }
                break;

            case ConsoleKey.LeftArrow:
                if (evt.Shift)
                {
                    // Start or extend selection
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                    if (CursorPosition > 0)
                    {
                        CursorPosition--;
                    }
                }
                else
                {
                    if (HasSelection)
                    {
                        // Move cursor to start of selection
                        CursorPosition = SelectionStart;
                        ClearSelection();
                    }
                    else if (CursorPosition > 0)
                    {
                        CursorPosition--;
                    }
                }
                break;

            case ConsoleKey.RightArrow:
                if (evt.Shift)
                {
                    // Start or extend selection
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                    if (CursorPosition < Text.Length)
                    {
                        CursorPosition++;
                    }
                }
                else
                {
                    if (HasSelection)
                    {
                        // Move cursor to end of selection
                        CursorPosition = SelectionEnd;
                        ClearSelection();
                    }
                    else if (CursorPosition < Text.Length)
                    {
                        CursorPosition++;
                    }
                }
                break;

            case ConsoleKey.Home:
                if (evt.Shift)
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                }
                else
                {
                    ClearSelection();
                }
                CursorPosition = 0;
                break;

            case ConsoleKey.End:
                if (evt.Shift)
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                }
                else
                {
                    ClearSelection();
                }
                CursorPosition = Text.Length;
                break;

            default:
                // Insert printable characters
                if (!char.IsControl(evt.KeyChar))
                {
                    // If there's a selection, delete it first
                    if (HasSelection)
                    {
                        DeleteSelection();
                    }
                    Text = Text.Insert(CursorPosition, evt.KeyChar.ToString());
                    CursorPosition++;
                }
                break;
        }
    }
}

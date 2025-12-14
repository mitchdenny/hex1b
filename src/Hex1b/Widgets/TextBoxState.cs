using Hex1b.Input;

namespace Hex1b.Widgets;

/// <summary>
/// Holds the mutable state for a TextBox. Create once and reuse across renders.
/// </summary>
public class TextBoxState
{
    private string _text = "";
    private int _cursorPosition = 0;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? "";
            // Clamp cursor position to valid range when text changes
            _cursorPosition = Math.Clamp(_cursorPosition, 0, _text.Length);
            // Also clamp selection anchor if it exists
            if (SelectionAnchor.HasValue)
            {
                SelectionAnchor = Math.Clamp(SelectionAnchor.Value, 0, _text.Length);
            }
        }
    }

    public int CursorPosition
    {
        get => _cursorPosition;
        set => _cursorPosition = Math.Clamp(value, 0, _text.Length);
    }
    
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
        _text = _text[..start] + _text[end..];
        _cursorPosition = start;
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

    /// <summary>
    /// Handles keyboard input for the text box.
    /// Returns true if the input was handled, false if it should be passed to parent containers.
    /// </summary>
    public bool HandleInput(Hex1bKeyEvent evt)
    {
        // Tab is never handled by TextBox - let it bubble up for focus navigation
        if (evt.Key == Hex1bKey.Tab)
        {
            return false;
        }

        // Ctrl+A: Select all
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Control) && evt.Key == Hex1bKey.A)
        {
            SelectAll();
            return true;
        }

        switch (evt.Key)
        {
            case Hex1bKey.Backspace:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition > 0)
                {
                    _text = _text.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                }
                return true;

            case Hex1bKey.Delete:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition < _text.Length)
                {
                    _text = _text.Remove(_cursorPosition, 1);
                }
                return true;

            case Hex1bKey.LeftArrow:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
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
                return true;

            case Hex1bKey.RightArrow:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
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
                return true;

            case Hex1bKey.Home:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
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
                return true;

            case Hex1bKey.End:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
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
                return true;

            default:
                // Insert printable characters
                if (!char.IsControl(evt.Character))
                {
                    // If there's a selection, delete it first
                    if (HasSelection)
                    {
                        DeleteSelection();
                    }
                    _text = _text.Insert(_cursorPosition, evt.Character.ToString());
                    _cursorPosition++;
                    return true;
                }
                // Non-printable, non-handled key
                return false;
        }
    }
}
